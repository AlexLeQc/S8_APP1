#nullable enable

using System.ComponentModel.DataAnnotations;

namespace CoupDeSonde.Api.DTOs;

/// <summary>
/// Input model for submitting a ballot via a single-use token.
/// </summary>
public sealed class SubmitVoteRequestDto
{
    /// <summary>
    /// The identifier of the survey being voted on.
    /// </summary>
    [Required]
    [RegularExpression(@"^[a-zA-Z0-9\-]+$", ErrorMessage = "Survey ID must contain only alphanumeric characters and hyphens.")]
    [MaxLength(100)]
    public string SurveyId { get; init; } = string.Empty;

    /// <summary>
    /// The single-use plaintext ballot token previously issued by <c>POST /api/v1/tokens</c>.
    /// The server hashes this with SHA-256 before any storage or comparison operations.
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(256)]
    public string BallotToken { get; init; } = string.Empty;

    /// <summary>
    /// A mapping of <c>QuestionId</c> to <c>ChoiceId</c> representing the voter's selections.
    /// Every question in the survey must be answered.
    /// </summary>
    [Required]
    public Dictionary<int, int> QuestionChoices { get; init; } = new();
}
