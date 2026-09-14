#nullable enable

using CoupDeSonde.Core.Models;
using CoupDeSonde.Core.Security;
using CoupDeSonde.Core.Services;
using CoupDeSonde.Core.Storage;
using Microsoft.Extensions.Options;

namespace CoupDeSonde.Tests;

/// <summary>
/// Integration-style tests for <see cref="VotingService"/> using real collaborators
/// (no mocks) backed by a temporary on-disk store, so the full security pipeline —
/// including atomic token consumption and anonymous vote persistence — is exercised.
/// </summary>
public sealed class VotingServiceTests : IDisposable
{
    private readonly string _baseDir;
    private readonly IFileStorage _storage;
    private readonly TokenService _tokenService;
    private readonly SurveyService _surveyService;
    private readonly VotingService _sut;

    // A reusable active survey fixture.
    private static readonly Survey _activeSurvey = new()
    {
        Id = "vote-survey",
        Title = "Voting Test Survey",
        Version = "1.0",
        IsActive = true,
        Questions =
        [
            new Question
            {
                Id = 1,
                Prompt = "Favourite colour?",
                Choices =
                [
                    new Choice { Id = 10, Text = "Red" },
                    new Choice { Id = 11, Text = "Blue" },
                ]
            }
        ]
    };

    private static readonly Survey _inactiveSurvey = new()
    {
        Id = "inactive-survey",
        Title = "Closed Survey",
        Version = "1.0",
        IsActive = false,
        Questions =
        [
            new Question
            {
                Id = 1,
                Prompt = "Q?",
                Choices = [ new Choice { Id = 10, Text = "A" } ]
            }
        ]
    };

    public VotingServiceTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "VotingSvc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_baseDir);

        _storage = new JsonFileStorage(
            Options.Create(new FileStorageOptions { BaseDirectory = _baseDir }));

        _tokenService  = new TokenService(_storage);
        _surveyService = new SurveyService(_storage);
        _sut           = new VotingService(_surveyService, _tokenService, _storage);
    }

    public void Dispose()
    {
        _sut.Dispose();
        if (Directory.Exists(_baseDir))
            Directory.Delete(_baseDir, recursive: true);
    }

    // ─── Constructor guards ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullSurveyService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new VotingService(null!, _tokenService, _storage));
    }

    [Fact]
    public void Constructor_NullTokenService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new VotingService(_surveyService, null!, _storage));
    }

    [Fact]
    public void Constructor_NullStorage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new VotingService(_surveyService, _tokenService, null!));
    }

    // ─── InvalidSubmission branch ─────────────────────────────────────────────

    [Fact]
    public async Task SubmitVoteAsync_NullSubmission_ReturnsInvalidSubmission()
    {
        VoteResult result = await _sut.SubmitVoteAsync(null!);
        Assert.Equal(VoteResult.InvalidSubmission, result);
    }

    [Fact]
    public async Task SubmitVoteAsync_EmptySurveyId_ReturnsInvalidSubmission()
    {
        var submission = new VoteSubmission
        {
            SurveyId       = "",
            BallotToken    = "some-token",
            QuestionChoices = new Dictionary<int, int> { [1] = 10 }
        };
        VoteResult result = await _sut.SubmitVoteAsync(submission);
        Assert.Equal(VoteResult.InvalidSubmission, result);
    }

    [Fact]
    public async Task SubmitVoteAsync_EmptyBallotToken_ReturnsInvalidSubmission()
    {
        var submission = new VoteSubmission
        {
            SurveyId       = "vote-survey",
            BallotToken    = "",
            QuestionChoices = new Dictionary<int, int> { [1] = 10 }
        };
        VoteResult result = await _sut.SubmitVoteAsync(submission);
        Assert.Equal(VoteResult.InvalidSubmission, result);
    }

    [Fact]
    public async Task SubmitVoteAsync_NullQuestionChoices_ReturnsInvalidSubmission()
    {
        var submission = new VoteSubmission
        {
            SurveyId       = "vote-survey",
            BallotToken    = "token",
            QuestionChoices = null!
        };
        VoteResult result = await _sut.SubmitVoteAsync(submission);
        Assert.Equal(VoteResult.InvalidSubmission, result);
    }

    // ─── SurveyNotFound branch ────────────────────────────────────────────────

    [Fact]
    public async Task SubmitVoteAsync_UnknownSurveyId_ReturnsSurveyNotFound()
    {
        var submission = new VoteSubmission
        {
            SurveyId        = "ghost-survey",
            BallotToken     = "any",
            QuestionChoices = new Dictionary<int, int> { [1] = 10 }
        };
        VoteResult result = await _sut.SubmitVoteAsync(submission);
        Assert.Equal(VoteResult.SurveyNotFound, result);
    }

    // ─── SurveyInactive branch ────────────────────────────────────────────────

    [Fact]
    public async Task SubmitVoteAsync_InactiveSurvey_ReturnsSurveyInactive()
    {
        await _surveyService.SaveSurveyAsync(_inactiveSurvey);

        var submission = new VoteSubmission
        {
            SurveyId        = "inactive-survey",
            BallotToken     = "any",
            QuestionChoices = new Dictionary<int, int> { [1] = 10 }
        };
        VoteResult result = await _sut.SubmitVoteAsync(submission);
        Assert.Equal(VoteResult.SurveyInactive, result);
    }

    // ─── InvalidChoices branch ────────────────────────────────────────────────

    [Fact]
    public async Task SubmitVoteAsync_InvalidChoices_ReturnsInvalidChoices()
    {
        await _surveyService.SaveSurveyAsync(_activeSurvey);

        var submission = new VoteSubmission
        {
            SurveyId        = "vote-survey",
            BallotToken     = "any",
            QuestionChoices = new Dictionary<int, int> { [999] = 888 } // bogus IDs
        };
        VoteResult result = await _sut.SubmitVoteAsync(submission);
        Assert.Equal(VoteResult.InvalidChoices, result);
    }

    // ─── InvalidToken branch ──────────────────────────────────────────────────

    [Fact]
    public async Task SubmitVoteAsync_TokenNotIssued_ReturnsInvalidToken()
    {
        await _surveyService.SaveSurveyAsync(_activeSurvey);

        var submission = new VoteSubmission
        {
            SurveyId        = "vote-survey",
            BallotToken     = "completely_fake_token",
            QuestionChoices = new Dictionary<int, int> { [1] = 10 }
        };
        VoteResult result = await _sut.SubmitVoteAsync(submission);
        Assert.Equal(VoteResult.InvalidToken, result);
    }

    // ─── Success: happy path ──────────────────────────────────────────────────

    [Fact]
    public async Task SubmitVoteAsync_ValidSubmission_ReturnsSuccess()
    {
        await _surveyService.SaveSurveyAsync(_activeSurvey);
        (string plaintext, _) = await _tokenService.IssueBallotTokenAsync("vote-survey");

        var submission = new VoteSubmission
        {
            SurveyId        = "vote-survey",
            BallotToken     = plaintext,
            QuestionChoices = new Dictionary<int, int> { [1] = 10 }
        };
        VoteResult result = await _sut.SubmitVoteAsync(submission);
        Assert.Equal(VoteResult.Success, result);
    }

    [Fact]
    public async Task SubmitVoteAsync_Success_AnonymousVoteRecordWrittenToDisk()
    {
        await _surveyService.SaveSurveyAsync(_activeSurvey);
        (string plaintext, _) = await _tokenService.IssueBallotTokenAsync("vote-survey");

        var submission = new VoteSubmission
        {
            SurveyId        = "vote-survey",
            BallotToken     = plaintext,
            QuestionChoices = new Dictionary<int, int> { [1] = 11 }
        };
        await _sut.SubmitVoteAsync(submission);

        // Exactly one vote file must exist under votes/vote-survey/.
        string votesDir = Path.Combine(_baseDir, "votes", "vote-survey");
        string[] voteFiles = Directory.GetFiles(votesDir, "*.json");
        Assert.Single(voteFiles);

        // The vote file must NOT contain the ballot token or its hash anywhere.
        string voteJson = await File.ReadAllTextAsync(voteFiles[0]);
        Assert.DoesNotContain(plaintext, voteJson);
        Assert.DoesNotContain("tokenHash", voteJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ballotToken", voteJson, StringComparison.OrdinalIgnoreCase);

        // The vote file must contain the expected choice.
        Assert.Contains("11", voteJson);
    }

    [Fact]
    public async Task SubmitVoteAsync_Success_BallotTokenMarkedConsumedOnDisk()
    {
        await _surveyService.SaveSurveyAsync(_activeSurvey);
        (string plaintext, BallotToken ballot) = await _tokenService.IssueBallotTokenAsync("vote-survey");

        var submission = new VoteSubmission
        {
            SurveyId        = "vote-survey",
            BallotToken     = plaintext,
            QuestionChoices = new Dictionary<int, int> { [1] = 10 }
        };
        await _sut.SubmitVoteAsync(submission);

        // Read the token back from disk and verify it is marked consumed.
        string tokenPath = Path.Combine(_baseDir, "tokens", "vote-survey", ballot.TokenHash + ".json");
        string tokenJson = await File.ReadAllTextAsync(tokenPath);
        Assert.Contains("\"isConsumed\": true", tokenJson);
    }

    // ─── Replay prevention: TokenAlreadyConsumed ──────────────────────────────

    [Fact]
    public async Task SubmitVoteAsync_ReusedToken_ReturnsTokenAlreadyConsumed()
    {
        await _surveyService.SaveSurveyAsync(_activeSurvey);
        (string plaintext, _) = await _tokenService.IssueBallotTokenAsync("vote-survey");

        var submission = new VoteSubmission
        {
            SurveyId        = "vote-survey",
            BallotToken     = plaintext,
            QuestionChoices = new Dictionary<int, int> { [1] = 10 }
        };

        // First vote: must succeed.
        VoteResult first = await _sut.SubmitVoteAsync(submission);
        Assert.Equal(VoteResult.Success, first);

        // Second vote with the same token: must be rejected as replay.
        VoteResult second = await _sut.SubmitVoteAsync(submission);
        Assert.Equal(VoteResult.TokenAlreadyConsumed, second);
    }

    [Fact]
    public async Task SubmitVoteAsync_ReplayAttack_OnlyOneVoteRecordPersisted()
    {
        await _surveyService.SaveSurveyAsync(_activeSurvey);
        (string plaintext, _) = await _tokenService.IssueBallotTokenAsync("vote-survey");

        var submission = new VoteSubmission
        {
            SurveyId        = "vote-survey",
            BallotToken     = plaintext,
            QuestionChoices = new Dictionary<int, int> { [1] = 10 }
        };

        await _sut.SubmitVoteAsync(submission); // Success
        await _sut.SubmitVoteAsync(submission); // TokenAlreadyConsumed

        string votesDir = Path.Combine(_baseDir, "votes", "vote-survey");
        string[] voteFiles = Directory.GetFiles(votesDir, "*.json");

        // Even after two attempts, only one record should exist.
        Assert.Single(voteFiles);
    }

    // ─── Race condition: concurrent identical token submissions ───────────────

    [Fact]
    public async Task SubmitVoteAsync_ConcurrentIdenticalToken_ExactlyOneSucceeds()
    {
        await _surveyService.SaveSurveyAsync(_activeSurvey);
        (string plaintext, _) = await _tokenService.IssueBallotTokenAsync("vote-survey");

        var submission = new VoteSubmission
        {
            SurveyId        = "vote-survey",
            BallotToken     = plaintext,
            QuestionChoices = new Dictionary<int, int> { [1] = 10 }
        };

        // Fire 10 concurrent requests all carrying the same token.
        const int concurrency = 10;
        Task<VoteResult>[] tasks = Enumerable
            .Range(0, concurrency)
            .Select(_ => _sut.SubmitVoteAsync(submission))
            .ToArray();

        VoteResult[] results = await Task.WhenAll(tasks);

        int successCount          = results.Count(r => r == VoteResult.Success);
        int alreadyConsumedCount  = results.Count(r => r == VoteResult.TokenAlreadyConsumed);

        // The semaphore must guarantee exactly one winner.
        Assert.Equal(1, successCount);
        Assert.Equal(concurrency - 1, alreadyConsumedCount);
    }

    [Fact]
    public async Task SubmitVoteAsync_ConcurrentIdenticalToken_OnlyOneVoteRecordPersisted()
    {
        await _surveyService.SaveSurveyAsync(_activeSurvey);
        (string plaintext, _) = await _tokenService.IssueBallotTokenAsync("vote-survey");

        var submission = new VoteSubmission
        {
            SurveyId        = "vote-survey",
            BallotToken     = plaintext,
            QuestionChoices = new Dictionary<int, int> { [1] = 10 }
        };

        await Task.WhenAll(Enumerable
            .Range(0, 20)
            .Select(_ => _sut.SubmitVoteAsync(submission)));

        string votesDir = Path.Combine(_baseDir, "votes", "vote-survey");
        string[] voteFiles = Directory.GetFiles(votesDir, "*.json");

        // Regardless of concurrency, only one anonymous VoteRecord must be on disk.
        Assert.Single(voteFiles);
    }

    [Fact]
    public async Task SubmitVoteAsync_ConcurrentDistinctTokens_AllSucceed()
    {
        await _surveyService.SaveSurveyAsync(_activeSurvey);

        const int voterCount = 5;

        // Issue one unique token per simulated voter.
        (string plaintext, BallotToken _)[] issued = await Task.WhenAll(
            Enumerable.Range(0, voterCount)
                .Select(_ => _tokenService.IssueBallotTokenAsync("vote-survey")));

        Task<VoteResult>[] tasks = issued.Select(t => _sut.SubmitVoteAsync(new VoteSubmission
        {
            SurveyId        = "vote-survey",
            BallotToken     = t.plaintext,
            QuestionChoices = new Dictionary<int, int> { [1] = 10 }
        })).ToArray();

        VoteResult[] results = await Task.WhenAll(tasks);

        // Every distinct-token voter must win.
        Assert.All(results, r => Assert.Equal(VoteResult.Success, r));

        // One vote record per voter.
        string votesDir = Path.Combine(_baseDir, "votes", "vote-survey");
        string[] voteFiles = Directory.GetFiles(votesDir, "*.json");
        Assert.Equal(voterCount, voteFiles.Length);
    }
}
