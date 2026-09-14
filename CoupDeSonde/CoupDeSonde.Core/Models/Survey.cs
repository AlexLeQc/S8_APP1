#nullable enable

namespace CoupDeSonde.Core.Models;

/// <summary>
/// Represents a survey containing one or more questions, and tracks its active status.
/// </summary>
public sealed class Survey
{
    /// <summary>
    /// Gets or sets the unique survey identifier.
    /// Should contain only alphanumeric characters and safe path characters.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the display title of the survey.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the version string for this survey definition.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the ordered list of questions in this survey.
    /// </summary>
    public List<Question> Questions { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether this survey is currently accepting responses.
    /// When <c>false</c>, vote submissions will be rejected.
    /// </summary>
    public bool IsActive { get; init; }
}
