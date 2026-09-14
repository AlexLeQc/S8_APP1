#nullable enable

namespace CoupDeSonde.Api.DTOs;

/// <summary>
/// Response model containing the single-use ballot token issued to an eligible voter.
/// The plaintext token is transmitted exactly once and never stored on the server
/// (only its SHA-256 hash is persisted — Deadly Sin #20 mitigation).
/// </summary>
public sealed class TokenResponseDto
{
    /// <summary>
    /// The raw plaintext cryptographic ballot token (64 hex characters / 256-bit entropy).
    /// The voter must present this token exactly once when submitting their vote.
    /// </summary>
    public string BallotToken { get; init; } = string.Empty;

    /// <summary>
    /// The survey identifier this ballot token is tied to.
    /// </summary>
    public string SurveyId { get; init; } = string.Empty;

    /// <summary>
    /// The UTC timestamp when this token was issued.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}
