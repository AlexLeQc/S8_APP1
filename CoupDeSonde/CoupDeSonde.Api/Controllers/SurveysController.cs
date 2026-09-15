#nullable enable

using Asp.Versioning;

using CoupDeSonde.Api.DTOs;
using CoupDeSonde.Core.Models;
using CoupDeSonde.Core.Services;
using CoupDeSonde.Core.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CoupDeSonde.Api.Controllers;

/// <summary>
/// Provides read-only access to survey resources.
/// </summary>
/// <remarks>
/// Surveys are managed exclusively as JSON files on the server file system.
/// Dynamic creation via API is disabled to reduce the attack surface.
/// <list type="bullet">
///   <item><description><c>GET /api/v1/surveys</c> — public; returns a summary list of all surveys.</description></item>
///   <item><description><c>GET /api/v1/surveys/{id}</c> — public; returns full survey detail.</description></item>
/// </list>
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/surveys")]
[Produces("application/json")]
public sealed class SurveysController : ControllerBase
{
    private readonly ISurveyService _surveyService;
    private readonly FileStorageOptions _storageOptions;
    private readonly ILogger<SurveysController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SurveysController"/>.
    /// </summary>
    public SurveysController(
        ISurveyService surveyService,
        IOptions<FileStorageOptions> storageOptions,
        ILogger<SurveysController> logger)
    {
        _surveyService = surveyService;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns a lightweight summary list of all available surveys.
    /// </summary>
    /// <returns>200 OK with a list of <see cref="SurveySummaryDto"/>.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SurveySummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSurveysAsync(CancellationToken cancellationToken)
    {
        string surveysDir = Path.Combine(_storageOptions.BaseDirectory, "surveys");

        if (!Directory.Exists(surveysDir))
        {
            _logger.LogDebug("Surveys directory does not exist yet; returning empty list.");
            return Ok(Array.Empty<SurveySummaryDto>());
        }

        var summaries = new List<SurveySummaryDto>();

        foreach (string file in Directory.EnumerateFiles(surveysDir, "*.json"))
        {
            // Extract the survey ID from the filename (without extension).
            string surveyId = Path.GetFileNameWithoutExtension(file);

            cancellationToken.ThrowIfCancellationRequested();

            Survey? survey = await _surveyService.GetSurveyAsync(surveyId, cancellationToken)
                .ConfigureAwait(false);

            if (survey is not null)
            {
                summaries.Add(new SurveySummaryDto
                {
                    Id = survey.Id,
                    Title = survey.Title,
                    Version = survey.Version,
                    IsActive = survey.IsActive,
                    QuestionCount = survey.Questions.Count,
                });
            }
        }

        return Ok(summaries);
    }

    /// <summary>
    /// Returns the full detail of a single survey, including all questions and choices.
    /// </summary>
    /// <param name="id">The unique survey identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the <see cref="Survey"/>, or 404 Not Found.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Survey), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSurveyByIdAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Problem(
                title: "Bad Request",
                detail: "Survey ID must not be empty.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        Survey? survey = await _surveyService.GetSurveyAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (survey is null)
        {
            _logger.LogDebug("Survey '{SurveyId}' not found.", id);
            return Problem(
                title: "Not Found",
                detail: $"Survey '{id}' does not exist.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(survey);
    }
}
