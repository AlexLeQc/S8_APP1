#nullable enable

using CoupDeSonde.Core.Models;
using CoupDeSonde.Core.Storage;

namespace CoupDeSonde.Core.Services;

/// <summary>
/// Implements survey persistence and vote choice validation via <see cref="IFileStorage"/>.
/// Surveys are stored as JSON files under <c>surveys/{surveyId}.json</c>.
/// </summary>
public sealed class SurveyService : ISurveyService
{
    // Survey storage path template: surveys/{surveyId}.json
    private const string SurveyPathTemplate = "surveys/{0}.json";

    private readonly IFileStorage _storage;

    /// <summary>
    /// Initializes a new instance of <see cref="SurveyService"/>.
    /// </summary>
    /// <param name="storage">The file storage used to persist survey definitions.</param>
    public SurveyService(IFileStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        _storage = storage;
    }

    /// <inheritdoc/>
    public async Task<Survey?> GetSurveyAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surveyId, nameof(surveyId));

        string path = string.Format(SurveyPathTemplate, surveyId);
        return await _storage.ReadAsync<Survey>(path, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SaveSurveyAsync(Survey survey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentException.ThrowIfNullOrWhiteSpace(survey.Id, nameof(survey));

        string path = string.Format(SurveyPathTemplate, survey.Id);
        await _storage.WriteAsync(path, survey, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Validation rules applied in order:
    /// <list type="number">
    ///   <item>Reject null or empty <paramref name="questionChoices"/>.</item>
    ///   <item>Count of answers must equal number of questions in the survey.</item>
    ///   <item>Every submitted question ID must exist in the survey.</item>
    ///   <item>Every submitted choice ID must exist within the matched question.</item>
    /// </list>
    /// All rules must pass for the method to return <c>true</c>.
    /// </remarks>
    public bool ValidateVoteChoices(Survey survey, IReadOnlyDictionary<int, int> questionChoices)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(questionChoices);

        // Rule 1: Submission must not be empty.
        if (questionChoices.Count == 0)
            return false;

        // Rule 2: Must answer every question — no more, no fewer.
        if (questionChoices.Count != survey.Questions.Count)
            return false;

        // Build a lookup keyed by question ID for O(1) access.
        Dictionary<int, Question> questionLookup = survey.Questions
            .ToDictionary(q => q.Id);

        foreach ((int questionId, int choiceId) in questionChoices)
        {
            // Rule 3: Question ID must exist in the survey.
            if (!questionLookup.TryGetValue(questionId, out Question? question))
                return false;

            // Rule 4: Choice ID must exist within that question.
            bool choiceExists = question.Choices.Any(c => c.Id == choiceId);
            if (!choiceExists)
                return false;
        }

        return true;
    }
}
