using CoupDeSonde.Api.Controllers;
using CoupDeSonde.Api.DTOs;
using CoupDeSonde.Core.Models;
using CoupDeSonde.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoupDeSonde.Tests;

public class VotesControllerTests
{
    private readonly Mock<IVotingService> _votingServiceMock = new();
    private readonly Mock<ILogger<VotesController>> _loggerMock = new();
    private readonly VotesController _controller;

    public VotesControllerTests()
    {
        _controller = new VotesController(_votingServiceMock.Object, _loggerMock.Object);
    }

    [Theory]
    [InlineData(VoteResult.Success, StatusCodes.Status200OK)]
    [InlineData(VoteResult.SurveyNotFound, StatusCodes.Status404NotFound)]
    [InlineData(VoteResult.SurveyInactive, StatusCodes.Status400BadRequest)]
    [InlineData(VoteResult.InvalidChoices, StatusCodes.Status400BadRequest)]
    [InlineData(VoteResult.InvalidSubmission, StatusCodes.Status400BadRequest)]
    [InlineData(VoteResult.InvalidToken, StatusCodes.Status401Unauthorized)]
    [InlineData(VoteResult.TokenAlreadyConsumed, StatusCodes.Status409Conflict)]
    [InlineData((VoteResult)999, StatusCodes.Status500InternalServerError)] // Default branch
    public async Task SubmitVoteAsync_MapsVoteResultToCorrectStatusCode(VoteResult result, int expectedStatusCode)
    {
        // Arrange
        var dto = new SubmitVoteRequestDto
        {
            SurveyId = "s1",
            BallotToken = "token",
            QuestionChoices = new Dictionary<int, int> { { 1, 1 } }
        };

        _votingServiceMock
            .Setup(v => v.SubmitVoteAsync(It.IsAny<VoteSubmission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.SubmitVoteAsync(dto, CancellationToken.None);

        // Assert
        if (expectedStatusCode == StatusCodes.Status200OK)
        {
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(expectedStatusCode, okResult.StatusCode);
        }
        else
        {
            var problemResult = Assert.IsType<ObjectResult>(actionResult);
            Assert.Equal(expectedStatusCode, problemResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
            Assert.Equal(expectedStatusCode, problem.Status);
        }
    }
}
