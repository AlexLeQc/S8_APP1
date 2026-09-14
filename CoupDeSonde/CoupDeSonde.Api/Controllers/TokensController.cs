#nullable enable

using Asp.Versioning;

using CoupDeSonde.Api.DTOs;
using CoupDeSonde.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoupDeSonde.Api.Controllers;

/// <summary>
/// Issues single-use cryptographic ballot tokens to eligible voters.
/// </summary>
/// <remarks>
/// <para>
/// <c>POST /api/v1/tokens</c> — publicly accessible. Accepts a survey ID and voter identifier,
/// verifies the survey exists and is active, then calls <see cref="CoupDeSonde.Core.Security.ITokenService"/>
/// to generate and persist a token (only the SHA-256 hash is stored — Deadly Sin #20).
/// The plaintext token is returned exactly once in the response and must be presented
/// by the voter when submitting their ballot.
/// </para>
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tokens")]
[Produces("application/json")]
public sealed class TokensController : ControllerBase
{
    private readonly CoupDeSonde.Core.Security.ITokenService _tokenService;
    private readonly ISurveyService _surveyService;
    private readonly ILogger<TokensController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="TokensController"/>.
    /// </summary>
    public TokensController(
        CoupDeSonde.Core.Security.ITokenService tokenService,
        ISurveyService surveyService,
        ILogger<TokensController> logger)
    {
        _tokenService = tokenService;
        _surveyService = surveyService;
        _logger = logger;
    }

    /// <summary>
    /// Issues a single-use ballot token for an eligible voter.
    /// </summary>
    /// <remarks>
    /// The plaintext token is returned once in this response. The server only stores
    /// the SHA-256 hash of the token (Deadly Sin #20 mitigation). The voter must present
    /// this token when submitting their vote via <c>POST /api/v1/votes</c>.
    /// </remarks>
    /// <param name="dto">The token issuance request containing the survey ID and voter identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// 201 Created with the one-time plaintext ballot token,
    /// 404 Not Found if the survey does not exist,
    /// or 400 Bad Request if the survey is inactive.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IssueTokenAsync(
        [FromBody] TokenRequestDto dto,
        CancellationToken cancellationToken)
    {
        var survey = await _surveyService.GetSurveyAsync(dto.SurveyId, cancellationToken)
            .ConfigureAwait(false);

        if (survey is null)
        {
            _logger.LogDebug("Token issuance failed: survey '{SurveyId}' not found.", dto.SurveyId);
            return Problem(
                title: "Not Found",
                detail: $"Survey '{dto.SurveyId}' does not exist.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!survey.IsActive)
        {
            _logger.LogDebug("Token issuance failed: survey '{SurveyId}' is inactive.", dto.SurveyId);
            return Problem(
                title: "Bad Request",
                detail: "The requested survey is not currently accepting votes.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        (string plaintextToken, var ballot) = await _tokenService
            .IssueBallotTokenAsync(dto.SurveyId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Ballot token issued for survey '{SurveyId}'. TokenHash={Hash}",
            dto.SurveyId,
            ballot.TokenHash);

        var response = new TokenResponseDto
        {
            BallotToken = plaintextToken,
            SurveyId = ballot.SurveyId,
            CreatedAt = ballot.CreatedAt,
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }
}
