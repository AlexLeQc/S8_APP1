#nullable enable

using System.Text;
using System.Security.Cryptography;
using CoupDeSonde.Core.Models;
using CoupDeSonde.Core.Security;
using CoupDeSonde.Core.Storage;
using Microsoft.Extensions.Options;
using Moq;

namespace CoupDeSonde.Tests;

/// <summary>
/// Tests for <see cref="TokenService"/>.
/// Covers: token entropy, deterministic SHA-256 output, and ballot issuance persistence.
/// </summary>
public sealed class TokenServiceTests : IDisposable
{
    private readonly string _baseDir;
    private readonly IFileStorage _storage;
    private readonly TokenService _sut;

    public TokenServiceTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "TokenSvc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_baseDir);

        _storage = new JsonFileStorage(
            Options.Create(new FileStorageOptions { BaseDirectory = _baseDir }));

        _sut = new TokenService(_storage);
    }

    public void Dispose()
    {
        if (Directory.Exists(_baseDir))
            Directory.Delete(_baseDir, recursive: true);
    }

    // ─── Constructor guards ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullStorage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TokenService(null!));
    }

    // ─── GenerateToken: high entropy ─────────────────────────────────────────

    [Fact]
    public void GenerateToken_Returns64CharHexString()
    {
        string token = _sut.GenerateToken();

        // 32 bytes → 64 hex characters
        Assert.Equal(64, token.Length);
        Assert.Matches("^[0-9a-f]{64}$", token);
    }

    [Fact]
    public void GenerateToken_IsLowercaseHex()
    {
        string token = _sut.GenerateToken();
        Assert.Equal(token, token.ToLowerInvariant());
    }

    [Fact]
    public void GenerateToken_ProducesNoDuplicatesAcross1000Calls()
    {
        // 256-bit entropy: the probability of any collision is astronomically small.
        HashSet<string> tokens = [];
        for (int i = 0; i < 1_000; i++)
            tokens.Add(_sut.GenerateToken());

        Assert.Equal(1_000, tokens.Count);
    }

    // ─── HashToken: deterministic SHA-256 ────────────────────────────────────

    [Fact]
    public void HashToken_IsDeterministic()
    {
        string token = "test_token_value";
        string hash1 = _sut.HashToken(token);
        string hash2 = _sut.HashToken(token);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashToken_Returns64CharLowercaseHex()
    {
        string hash = _sut.HashToken("any_value");

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void HashToken_MatchesReferenceImplementation()
    {
        const string token = "hello_world";
        byte[] expectedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        string expectedHex = Convert.ToHexString(expectedBytes).ToLowerInvariant();

        string actual = _sut.HashToken(token);

        Assert.Equal(expectedHex, actual);
    }

    [Fact]
    public void HashToken_DifferentInputsProduceDifferentHashes()
    {
        string h1 = _sut.HashToken("token_A");
        string h2 = _sut.HashToken("token_B");
        Assert.NotEqual(h1, h2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HashToken_NullOrWhitespace_ThrowsArgumentException(string token)
    {
        Assert.Throws<ArgumentException>(() => _sut.HashToken(token));
    }

    // ─── IssueBallotTokenAsync ────────────────────────────────────────────────

    [Fact]
    public async Task IssueBallotTokenAsync_ReturnsNonEmptyPlaintextToken()
    {
        (string plaintext, BallotToken ballot) = await _sut.IssueBallotTokenAsync("survey-1");

        Assert.False(string.IsNullOrWhiteSpace(plaintext));
    }

    [Fact]
    public async Task IssueBallotTokenAsync_BallotHashMatchesHashOfPlaintext()
    {
        (string plaintext, BallotToken ballot) = await _sut.IssueBallotTokenAsync("survey-1");

        string expectedHash = _sut.HashToken(plaintext);
        Assert.Equal(expectedHash, ballot.TokenHash);
    }

    [Fact]
    public async Task IssueBallotTokenAsync_PlaintextIsNotStoredInBallot()
    {
        (string plaintext, BallotToken ballot) = await _sut.IssueBallotTokenAsync("survey-1");

        // The ballot must NOT contain the raw plaintext anywhere.
        Assert.NotEqual(plaintext, ballot.TokenHash);
        Assert.False(string.IsNullOrWhiteSpace(ballot.TokenHash));
    }

    [Fact]
    public async Task IssueBallotTokenAsync_BallotIsPersistedToDisk()
    {
        (string plaintext, BallotToken ballot) = await _sut.IssueBallotTokenAsync("survey-42");

        string expectedPath = Path.Combine(_baseDir, "tokens", "survey-42", ballot.TokenHash + ".json");
        Assert.True(File.Exists(expectedPath), "BallotToken JSON file was not created on disk.");
    }

    [Fact]
    public async Task IssueBallotTokenAsync_BallotIsInitiallyNotConsumed()
    {
        (_, BallotToken ballot) = await _sut.IssueBallotTokenAsync("survey-1");

        Assert.False(ballot.IsConsumed);
        Assert.Null(ballot.ConsumedAt);
    }

    [Fact]
    public async Task IssueBallotTokenAsync_BallotSurveyIdMatchesInput()
    {
        (_, BallotToken ballot) = await _sut.IssueBallotTokenAsync("my-survey");

        Assert.Equal("my-survey", ballot.SurveyId);
    }

    [Fact]
    public async Task IssueBallotTokenAsync_EmptySurveyId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.IssueBallotTokenAsync(""));
    }

    [Fact]
    public async Task IssueBallotTokenAsync_EachCallProducesUniqueToken()
    {
        (string t1, _) = await _sut.IssueBallotTokenAsync("s");
        (string t2, _) = await _sut.IssueBallotTokenAsync("s");

        Assert.NotEqual(t1, t2);
    }
}
