#nullable enable

namespace CoupDeSonde.Core.Models;

/// <summary>
/// Represents a single question within a survey, containing one or more choices.
/// </summary>
public sealed class Question
{
    /// <summary>
    /// Gets or sets the unique identifier of this question within the survey.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets or sets the question prompt text displayed to voters.
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of available answer choices.
    /// Must contain at least one choice.
    /// </summary>
    public List<Choice> Choices { get; init; } = [];
}
