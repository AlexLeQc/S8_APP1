#nullable enable

using CoupDeSonde.Core.Models;

namespace CoupDeSonde.Core.Services;

/// <summary>
/// Manages survey persistence and validates vote choices against a survey's structure.
/// </summary>
public interface ISurveyService
{
    /// <summary>
    /// Retrieves the survey with the given identifier, or <c>null</c> if it does not exist.
    /// </summary>
    /// <param name="surveyId">The unique survey identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="Survey"/> entity, or <c>null</c> if not found.</returns>
    Task<Survey?> GetSurveyAsync(string surveyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a survey definition to storage.
    /// </summary>
    /// <param name="survey">The survey to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveSurveyAsync(Survey survey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that <paramref name="questionChoices"/> contains a valid, complete set of
    /// answers for every question in <paramref name="survey"/>:
    /// <list type="bullet">
    ///   <item>The collection is not null or empty.</item>
    ///   <item>Every question in the survey has exactly one answer.</item>
    ///   <item>Every question ID in the submission exists in the survey.</item>
    ///   <item>Every selected choice ID exists within the corresponding question.</item>
    /// </list>
    /// </summary>
    /// <param name="survey">The survey whose structure is validated against.</param>
    /// <param name="questionChoices">A map of question ID to selected choice ID.</param>
    /// <returns><c>true</c> if all choices are valid; otherwise <c>false</c>.</returns>
    bool ValidateVoteChoices(Survey survey, IReadOnlyDictionary<int, int> questionChoices);
}
