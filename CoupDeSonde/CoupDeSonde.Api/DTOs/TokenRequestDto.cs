#nullable enable

using System.ComponentModel.DataAnnotations;

namespace CoupDeSonde.Api.DTOs;

/// <summary>
/// Input model for requesting a single-use ballot token for an eligible voter.
/// </summary>
public sealed class TokenRequestDto
{
    /// <summary>
    /// The identifier of the survey for which a ballot token is being requested.
    /// </summary>
    [Required]
    [RegularExpression(@"^[a-zA-Z0-9\-]+$", ErrorMessage = "Survey ID must contain only alphanumeric characters and hyphens.")]
    [MaxLength(100)]
    public string SurveyId { get; init; } = string.Empty;

    /// <summary>
    /// An opaque identifier for the voter (e.g., student ID, employee number).
    /// This is used by the issuing authority to verify eligibility but is NOT
    /// stored alongside the vote to preserve ballot anonymity.
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string VoterIdentifier { get; init; } = string.Empty;
}
