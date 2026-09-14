#nullable enable

namespace CoupDeSonde.Api.DTOs;

/// <summary>
/// Lightweight survey summary returned in list operations.
/// Exposes only non-sensitive metadata without question details.
/// </summary>
public sealed class SurveySummaryDto
{
    /// <summary>
    /// Unique survey identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable survey title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Survey version string.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Whether the survey is currently accepting vote submissions.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Total number of questions in the survey.
    /// </summary>
    public int QuestionCount { get; init; }
}
