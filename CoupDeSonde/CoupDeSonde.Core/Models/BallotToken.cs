#nullable enable

namespace CoupDeSonde.Core.Models;

/// <summary>
/// Represents a single-use cryptographic ballot token issued to a voter.
/// The plaintext token is never stored — only its SHA-256 hash is persisted,
/// preventing correlation of voter identity with cast ballots.
/// </summary>
public sealed class BallotToken
{
    /// <summary>
    /// Gets or sets the SHA-256 hex-encoded hash of the original plaintext token.
    /// This is the primary key used for storage and lookup.
    /// </summary>
    public string TokenHash { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the survey this ballot is valid for.
    /// </summary>
    public string SurveyId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when this ballot token was issued.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this token has already been used to cast a vote.
    /// Once consumed, the token must never be accepted again (prevents replay attacks).
    /// </summary>
    public bool IsConsumed { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this token was consumed (vote was cast).
    /// <c>null</c> if the token has not yet been used.
    /// </summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}
