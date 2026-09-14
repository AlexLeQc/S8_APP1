#nullable enable

using Asp.Versioning;

using CoupDeSonde.Api.DTOs;
using CoupDeSonde.Core.Models;
using CoupDeSonde.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoupDeSonde.Api.Controllers;

/// <summary>
/// Accepts anonymous ballot submissions.
/// </summary>
/// <remarks>
/// <para>
/// <c>POST /api/v1/votes</c> — publicly accessible. Accepts a vote payload containing a
/// single-use ballot token and question choices. Delegates to <see cref="IVotingService"/>
/// which performs all validation atomically (replay attack prevention via semaphore — Deadly Sin #13).
/// </para>
/// <para>
/// HTTP status codes map directly from the <see cref="VoteResult"/> enum:
/// </para>
/// <list type="table">
///   <listheader><term>VoteResult</term><description>HTTP Status</description></listheader>
///   <item><term>Success</term><description>200 OK</description></item>
///   <item><term>SurveyNotFound</term><description>404 Not Found</description></item>
///   <item><term>SurveyInactive</term><description>400 Bad Request</description></item>
///   <item><term>InvalidChoices</term><description>400 Bad Request</description></item>
///   <item><term>InvalidSubmission</term><description>400 Bad Request</description></item>
///   <item><term>InvalidToken</term><description>401 Unauthorized</description></item>
///   <item><term>TokenAlreadyConsumed</term><description>409 Conflict</description></item>
/// </list>
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/votes")]
[Produces("application/json")]
public sealed class VotesController : ControllerBase
{
    private readonly IVotingService _votingService;
    private readonly ILogger<VotesController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="VotesController"/>.
    /// </summary>
    public VotesController(IVotingService votingService, ILogger<VotesController> logger)
    {
        _votingService = votingService;
        _logger = logger;
    }

    /// <summary>
    /// Submits a ballot vote using a single-use token.
    /// </summary>
    /// <remarks>
    /// The ballot token is consumed atomically. Concurrent submissions using the same token
    /// are safely rejected (race-condition protection — Deadly Sin #13).
    /// The stored vote record does not contain the token or voter identity (secret ballot).
    /// </remarks>
    /// <param name="dto">The vote submission payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK on success, or an appropriate error status with <see cref="ProblemDetails"/>.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitVoteAsync(
        [FromBody] SubmitVoteRequestDto dto,
        CancellationToken cancellationToken)
    {
        // Map DTO → Core domain model.
        var submission = new VoteSubmission
        {
            SurveyId = dto.SurveyId,
            BallotToken = dto.BallotToken,
            QuestionChoices = dto.QuestionChoices,
        };

        VoteResult result = await _votingService
            .SubmitVoteAsync(submission, cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            VoteResult.Success =>
                Ok(new { message = "Vote cast successfully." }),

            VoteResult.SurveyNotFound =>
                Problem(
                    title: "Not Found",
                    detail: $"Survey '{dto.SurveyId}' does not exist.",
                    statusCode: StatusCodes.Status404NotFound),

            VoteResult.SurveyInactive =>
                Problem(
                    title: "Bad Request",
                    detail: "The survey is not currently accepting votes.",
                    statusCode: StatusCodes.Status400BadRequest),

            VoteResult.InvalidChoices =>
                Problem(
                    title: "Bad Request",
                    detail: "One or more question/choice selections are invalid for this survey.",
                    statusCode: StatusCodes.Status400BadRequest),

            VoteResult.InvalidSubmission =>
                Problem(
                    title: "Bad Request",
                    detail: "The vote submission payload is malformed or incomplete.",
                    statusCode: StatusCodes.Status400BadRequest),

            VoteResult.InvalidToken =>
                Problem(
                    title: "Unauthorized",
                    detail: "The provided ballot token is invalid.",
                    statusCode: StatusCodes.Status401Unauthorized),

            VoteResult.TokenAlreadyConsumed =>
                Problem(
                    title: "Conflict",
                    detail: "This ballot token has already been used. Replay attack prevented.",
                    statusCode: StatusCodes.Status409Conflict),

            // Defensive default for any future enum values.
            _ => Problem(
                    title: "Internal Server Error",
                    detail: "An unexpected voting error occurred.",
                    statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}
