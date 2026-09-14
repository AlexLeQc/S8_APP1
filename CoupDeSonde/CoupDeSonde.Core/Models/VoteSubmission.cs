#nullable enable

namespace CoupDeSonde.Core.Models;

/// <summary>
/// Represents the data submitted by a voter when casting their ballot.
/// Contains the raw plaintext token (used once for verification, never stored)
/// and the selected choices per question.
/// </summary>
public sealed class VoteSubmission
{
    /// <summary>
    /// Gets or sets the identifier of the survey being voted on.
    /// </summary>
    public string SurveyId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the mapping of question IDs to the voter's selected choice IDs.
    /// Key: QuestionId, Value: ChoiceId.
    /// </summary>
    public Dictionary<int, int> QuestionChoices { get; init; } = [];

    /// <summary>
    /// Gets or sets the raw plaintext cryptographic token provided by the voter.
    /// This value is hashed before any lookup or storage; the plaintext is never persisted.
    /// </summary>
    public string BallotToken { get; init; } = string.Empty;
}
