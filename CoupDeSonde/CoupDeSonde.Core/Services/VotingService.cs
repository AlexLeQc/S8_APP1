#nullable enable

using System.Collections.Concurrent;
using CoupDeSonde.Core.Models;
using CoupDeSonde.Core.Security;
using CoupDeSonde.Core.Storage;

namespace CoupDeSonde.Core.Services;

/// <summary>
/// Processes vote submissions with full security guarantees:
/// <list type="bullet">
///   <item>Token replay / double-voting prevention (Deadly Sin #13):
///     An in-memory <see cref="SemaphoreSlim"/> keyed by token hash ensures that
///     concurrent submissions carrying the same token are serialized.
///     Only the first request that successfully reads an un-consumed token wins;
///     all subsequent identical tokens are rejected with
///     <see cref="VoteResult.TokenAlreadyConsumed"/>.</item>
///   <item>Voter anonymity: the persisted <see cref="VoteRecord"/> contains no
///     reference to the ballot token or its hash, making vote-to-voter correlation
///     impossible from storage alone.</item>
/// </list>
/// </summary>
public sealed class VotingService : IVotingService, IDisposable
{
    // Path templates
    private const string TokenPathTemplate = "tokens/{0}/{1}.json";
    private const string VotePathTemplate  = "votes/{0}/{1}.json";

    private readonly ISurveyService _surveyService;
    private readonly ITokenService  _tokenService;
    private readonly IFileStorage   _storage;

    // Per-token-hash semaphores (Deadly Sin #13 mitigation).
    // A SemaphoreSlim(1,1) per token hash guarantees that at most one thread
    // executes the critical section (read → check IsConsumed → write) for
    // any given token hash at a time.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _tokenLocks = new();

    /// <summary>
    /// Initializes a new instance of <see cref="VotingService"/>.
    /// </summary>
    public VotingService(
        ISurveyService surveyService,
        ITokenService  tokenService,
        IFileStorage   storage)
    {
        ArgumentNullException.ThrowIfNull(surveyService);
        ArgumentNullException.ThrowIfNull(tokenService);
        ArgumentNullException.ThrowIfNull(storage);

        _surveyService = surveyService;
        _tokenService  = tokenService;
        _storage       = storage;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Processing pipeline (any failure short-circuits immediately):
    /// <list type="number">
    ///   <item>Validate that <paramref name="submission"/> is well-formed.</item>
    ///   <item>Load the survey; reject if not found or inactive.</item>
    ///   <item>Validate question/choice selections against the survey structure.</item>
    ///   <item>Hash the ballot token (plaintext is discarded after this point).</item>
    ///   <item>Acquire the per-token-hash semaphore — serialize concurrent requests
    ///     for the same token (Deadly Sin #13 mitigation).</item>
    ///   <item>Load the <see cref="BallotToken"/> record; reject if missing or consumed.</item>
    ///   <item>Mark the token as consumed and persist atomically.</item>
    ///   <item>Persist an anonymous <see cref="VoteRecord"/> (no token data included).</item>
    ///   <item>Release the semaphore.</item>
    /// </list>
    /// </remarks>
    public async Task<VoteResult> SubmitVoteAsync(
        VoteSubmission submission,
        CancellationToken cancellationToken = default)
    {
        // ── Step 1: Input validation ─────────────────────────────────────────
        if (submission is null
            || string.IsNullOrWhiteSpace(submission.SurveyId)
            || string.IsNullOrWhiteSpace(submission.BallotToken)
            || submission.QuestionChoices is null)
        {
            return VoteResult.InvalidSubmission;
        }

        // ── Step 2: Load and validate the survey ─────────────────────────────
        Survey? survey = await _surveyService
            .GetSurveyAsync(submission.SurveyId, cancellationToken)
            .ConfigureAwait(false);

        if (survey is null)
            return VoteResult.SurveyNotFound;

        if (!survey.IsActive)
            return VoteResult.SurveyInactive;

        // ── Step 3: Validate question/choice selections ───────────────────────
        if (!_surveyService.ValidateVoteChoices(survey, submission.QuestionChoices))
            return VoteResult.InvalidChoices;

        // ── Step 4: Hash the token (plaintext no longer needed after this) ────
        string tokenHash = _tokenService.HashToken(submission.BallotToken);
        string tokenPath = string.Format(TokenPathTemplate, submission.SurveyId, tokenHash);

        // ── Step 5: Acquire per-token semaphore (race-condition guard) ────────
        //    GetOrAdd is atomic; the lambda executes at most once per key.
        SemaphoreSlim semaphore = _tokenLocks.GetOrAdd(
            tokenHash,
            static _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // ── Step 6: Load the ballot token record ──────────────────────────
            BallotToken? ballot = await _storage
                .ReadAsync<BallotToken>(tokenPath, cancellationToken)
                .ConfigureAwait(false);

            if (ballot is null)
                return VoteResult.InvalidToken;

            if (ballot.IsConsumed)
                return VoteResult.TokenAlreadyConsumed;

            // ── Step 7: Mark token as consumed ────────────────────────────────
            //    Mutation is intentional: BallotToken.IsConsumed/ConsumedAt have
            //    setters specifically for this lifecycle transition.
            ballot.IsConsumed = true;
            ballot.ConsumedAt = DateTimeOffset.UtcNow;

            await _storage.WriteAsync(tokenPath, ballot, cancellationToken)
                .ConfigureAwait(false);

            // ── Step 8: Persist anonymous vote record (NO token data stored) ──
            //    VoteRecord deliberately omits TokenHash and BallotToken to ensure
            //    that no correlation between a voter and their ballot is possible.
            VoteRecord voteRecord = new()
            {
                Id             = Guid.NewGuid().ToString("D"),
                SurveyId       = submission.SurveyId,
                QuestionChoices = new Dictionary<int, int>(submission.QuestionChoices),
                Timestamp      = DateTimeOffset.UtcNow,
            };

            string votePath = string.Format(VotePathTemplate, submission.SurveyId, voteRecord.Id);
            await _storage.WriteAsync(votePath, voteRecord, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            // ── Step 9: Always release the semaphore ──────────────────────────
            semaphore.Release();
        }

        return VoteResult.Success;
    }

    /// <summary>
    /// Disposes all per-token semaphores held by this instance.
    /// </summary>
    public void Dispose()
    {
        foreach (SemaphoreSlim semaphore in _tokenLocks.Values)
            semaphore.Dispose();

        _tokenLocks.Clear();
    }
}
