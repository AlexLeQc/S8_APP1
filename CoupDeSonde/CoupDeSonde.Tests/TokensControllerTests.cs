using CoupDeSonde.Api.Controllers;
using CoupDeSonde.Api.DTOs;
using CoupDeSonde.Core.Models;
using CoupDeSonde.Core.Security;
using CoupDeSonde.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoupDeSonde.Tests;

public class TokensControllerTests
{
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<ISurveyService> _surveyServiceMock = new();
    private readonly Mock<ILogger<TokensController>> _loggerMock = new();
    private readonly TokensController _controller;

    public TokensControllerTests()
    {
        _controller = new TokensController(_tokenServiceMock.Object, _surveyServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task IssueTokenAsync_SurveyNotFound_ReturnsNotFound()
    {
        // Arrange
        var dto = new TokenRequestDto { SurveyId = "s1", VoterIdentifier = "v1" };
        _surveyServiceMock.Setup(s => s.GetSurveyAsync("s1", It.IsAny<CancellationToken>())).ReturnsAsync((Survey?)null);

        // Act
        var result = await _controller.IssueTokenAsync(dto, CancellationToken.None);

        // Assert
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objResult.Value);
        Assert.Equal("Survey 's1' does not exist.", problem.Detail);
    }

    [Fact]
    public async Task IssueTokenAsync_SurveyInactive_ReturnsBadRequest()
    {
        // Arrange
        var dto = new TokenRequestDto { SurveyId = "s1", VoterIdentifier = "v1" };
        var survey = new Survey { Id = "s1", IsActive = false };
        _surveyServiceMock.Setup(s => s.GetSurveyAsync("s1", It.IsAny<CancellationToken>())).ReturnsAsync(survey);

        // Act
        var result = await _controller.IssueTokenAsync(dto, CancellationToken.None);

        // Assert
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objResult.Value);
        Assert.Equal("The requested survey is not currently accepting votes.", problem.Detail);
    }

    [Fact]
    public async Task IssueTokenAsync_Success_Returns201WithToken()
    {
        // Arrange
        var dto = new TokenRequestDto { SurveyId = "s1", VoterIdentifier = "v1" };
        var survey = new Survey { Id = "s1", IsActive = true };
        _surveyServiceMock.Setup(s => s.GetSurveyAsync("s1", It.IsAny<CancellationToken>())).ReturnsAsync(survey);

        var ballot = new BallotToken { SurveyId = "s1", TokenHash = "hash", CreatedAt = DateTimeOffset.UtcNow };
        _tokenServiceMock.Setup(t => t.IssueBallotTokenAsync("s1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(("plaintext-token", ballot));

        // Act
        var result = await _controller.IssueTokenAsync(dto, CancellationToken.None);

        // Assert
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objResult.StatusCode);
        var responseDto = Assert.IsType<TokenResponseDto>(objResult.Value);
        Assert.Equal("plaintext-token", responseDto.BallotToken);
        Assert.Equal("s1", responseDto.SurveyId);
        Assert.Equal(ballot.CreatedAt, responseDto.CreatedAt);
    }
}
