#nullable enable

namespace CoupDeSonde.Core.Models;

/// <summary>
/// Represents a fully anonymized, persisted vote record.
/// This record contains no reference to the ballot token, voter identity,
/// or any data that could be used to correlate a vote with a specific voter.
/// </summary>
public sealed class VoteRecord
{
    /// <summary>
    /// Gets or sets the unique identifier (GUID) for this vote record.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the survey this vote is associated with.
    /// </summary>
    public string SurveyId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the anonymized question-choice selections made by the voter.
    /// Key: QuestionId, Value: ChoiceId.
    /// </summary>
    public Dictionary<int, int> QuestionChoices { get; init; } = [];

    /// <summary>
    /// Gets or sets the UTC timestamp when the vote was submitted and recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}
