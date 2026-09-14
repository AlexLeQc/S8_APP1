#nullable enable

namespace CoupDeSonde.Core.Services;

/// <summary>
/// Discriminated union of outcomes for a vote submission attempt.
/// </summary>
public enum VoteResult
{
    /// <summary>Vote was accepted and persisted successfully.</summary>
    Success,

    /// <summary>No survey was found for the given survey ID.</summary>
    SurveyNotFound,

    /// <summary>The survey exists but is not currently accepting responses.</summary>
    SurveyInactive,

    /// <summary>The provided question/choice selections are structurally invalid for the survey.</summary>
    InvalidChoices,

    /// <summary>The provided ballot token does not correspond to any issued token.</summary>
    InvalidToken,

    /// <summary>The ballot token is valid but has already been used to cast a vote.</summary>
    TokenAlreadyConsumed,

    /// <summary>The submission itself is malformed (e.g., null required fields).</summary>
    InvalidSubmission,
}
