#nullable enable

using System.ComponentModel.DataAnnotations;

namespace CoupDeSonde.Api.DTOs;

/// <summary>
/// Input model for creating a new survey (admin-only endpoint).
/// </summary>
public sealed class CreateSurveyRequestDto
{
    /// <summary>
    /// Unique alphanumeric survey identifier. Must be URL-safe (letters, digits, hyphens only).
    /// </summary>
    [Required]
    [RegularExpression(@"^[a-zA-Z0-9\-]+$", ErrorMessage = "Survey ID must contain only alphanumeric characters and hyphens.")]
    [MaxLength(100)]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable title displayed to voters.
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(300)]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Version identifier for the survey document (e.g., "1.0", "2024-fall").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Ordered list of questions; must contain at least one question.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<CreateQuestionDto> Questions { get; init; } = new();

    /// <summary>
    /// Whether the survey immediately accepts responses upon creation.
    /// </summary>
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// DTO for a single survey question during survey creation.
/// </summary>
public sealed class CreateQuestionDto
{
    /// <summary>
    /// Unique question identifier within this survey.
    /// </summary>
    [Required]
    public int Id { get; init; }

    /// <summary>
    /// The prompt displayed to the voter.
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(1000)]
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// Available answer choices; must contain at least one.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<CreateChoiceDto> Choices { get; init; } = new();
}

/// <summary>
/// DTO for a single answer choice during survey creation.
/// </summary>
public sealed class CreateChoiceDto
{
    /// <summary>
    /// Unique choice identifier within its parent question.
    /// </summary>
    [Required]
    public int Id { get; init; }

    /// <summary>
    /// The label text for this choice.
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(500)]
    public string Text { get; init; } = string.Empty;
}
