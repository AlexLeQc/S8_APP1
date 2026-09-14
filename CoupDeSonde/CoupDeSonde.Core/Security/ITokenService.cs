#nullable enable

using CoupDeSonde.Core.Models;

namespace CoupDeSonde.Core.Security;

/// <summary>
/// Provides cryptographic ballot token generation, hashing, and issuance operations.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a new cryptographically-secure random token.
    /// The returned value is the raw plaintext token; it must be sent to the voter
    /// exactly once and must never be stored.
    /// </summary>
    /// <returns>A hex-encoded, 256-bit random token string.</returns>
    string GenerateToken();

    /// <summary>
    /// Computes the SHA-256 hash of <paramref name="token"/> and returns it as a
    /// lowercase hex string. This is the value stored for later verification.
    /// </summary>
    /// <param name="token">The plaintext token to hash.</param>
    /// <returns>A 64-character lowercase hex string (SHA-256 digest).</returns>
    string HashToken(string token);

    /// <summary>
    /// Issues a new ballot token for the specified survey: generates the plaintext token,
    /// hashes it, persists the <see cref="BallotToken"/> record, and returns both values.
    /// The plaintext token is returned only once to be delivered to the voter.
    /// </summary>
    /// <param name="surveyId">The survey to which the ballot token is tied.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A tuple containing the one-time plaintext token (to transmit to the voter)
    /// and the persisted <see cref="BallotToken"/> entity (stored by hash only).
    /// </returns>
    Task<(string PlaintextToken, BallotToken Ballot)> IssueBallotTokenAsync(
        string surveyId,
        CancellationToken cancellationToken = default);
}
