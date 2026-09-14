#nullable enable

namespace CoupDeSonde.Core.Models;

/// <summary>
/// Represents a single selectable option within a survey question.
/// </summary>
public sealed class Choice
{
    /// <summary>
    /// Gets or sets the unique identifier of this choice within its parent question.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets or sets the display text/label for this choice.
    /// Must not be null or empty.
    /// </summary>
    public string Text { get; init; } = string.Empty;
}
