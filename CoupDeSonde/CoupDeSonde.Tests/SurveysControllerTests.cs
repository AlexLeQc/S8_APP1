using Microsoft.AspNetCore.Http;
using CoupDeSonde.Api.Controllers;
using CoupDeSonde.Api.DTOs;
using CoupDeSonde.Core.Models;
using CoupDeSonde.Core.Services;
using CoupDeSonde.Core.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CoupDeSonde.Tests;

public class SurveysControllerTests : IDisposable
{
    private readonly Mock<ISurveyService> _surveyServiceMock = new();
    private readonly Mock<ILogger<SurveysController>> _loggerMock = new();
    private readonly string _tempDirectory;
    private readonly SurveysController _controller;

    public SurveysControllerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        
        var optionsMock = new Mock<IOptions<FileStorageOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new FileStorageOptions { BaseDirectory = _tempDirectory });

        _controller = new SurveysController(_surveyServiceMock.Object, optionsMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public async Task GetAllSurveysAsync_DirectoryDoesNotExist_ReturnsEmptyList()
    {
        // Act
        var result = await _controller.GetAllSurveysAsync(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var summaries = Assert.IsAssignableFrom<IEnumerable<SurveySummaryDto>>(okResult.Value);
        Assert.Empty(summaries);
    }

    [Fact]
    public async Task GetAllSurveysAsync_ReturnsSummaries()
    {
        // Arrange
        var surveysDir = Path.Combine(_tempDirectory, "surveys");
        Directory.CreateDirectory(surveysDir);
        File.WriteAllText(Path.Combine(surveysDir, "s1.json"), "{}");
        File.WriteAllText(Path.Combine(surveysDir, "s2.json"), "{}");

        var survey1 = new Survey { Id = "s1", Title = "Title1", Version = "1", IsActive = true, Questions = { new Question() } };
        
        _surveyServiceMock.Setup(s => s.GetSurveyAsync("s1", It.IsAny<CancellationToken>())).ReturnsAsync(survey1);
        _surveyServiceMock.Setup(s => s.GetSurveyAsync("s2", It.IsAny<CancellationToken>())).ReturnsAsync((Survey?)null);

        // Act
        var result = await _controller.GetAllSurveysAsync(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var summaries = Assert.IsAssignableFrom<IEnumerable<SurveySummaryDto>>(okResult.Value).ToList();
        
        Assert.Single(summaries); // "s2" returns null, so only 1 added
        Assert.Equal("s1", summaries[0].Id);
        Assert.Equal("Title1", summaries[0].Title);
        Assert.Equal("1", summaries[0].Version);
        Assert.True(summaries[0].IsActive);
        Assert.Equal(1, summaries[0].QuestionCount);
    }

    [Fact]
    public async Task GetSurveyByIdAsync_EmptyId_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetSurveyByIdAsync("   ", CancellationToken.None);

        // Assert
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objResult.Value);
        Assert.Equal("Survey ID must not be empty.", problem.Detail);
    }

    [Fact]
    public async Task GetSurveyByIdAsync_SurveyNotFound_ReturnsNotFound()
    {
        // Arrange
        _surveyServiceMock.Setup(s => s.GetSurveyAsync("s1", It.IsAny<CancellationToken>())).ReturnsAsync((Survey?)null);

        // Act
        var result = await _controller.GetSurveyByIdAsync("s1", CancellationToken.None);

        // Assert
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objResult.Value);
        Assert.Equal("Survey 's1' does not exist.", problem.Detail);
    }

    [Fact]
    public async Task GetSurveyByIdAsync_SurveyExists_ReturnsOk()
    {
        // Arrange
        var survey = new Survey { Id = "s1" };
        _surveyServiceMock.Setup(s => s.GetSurveyAsync("s1", It.IsAny<CancellationToken>())).ReturnsAsync(survey);

        // Act
        var result = await _controller.GetSurveyByIdAsync("s1", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(survey, okResult.Value);
    }

    [Fact]
    public async Task CreateSurveyAsync_CreatesAndReturns201()
    {
        // Arrange
        var dto = new CreateSurveyRequestDto
        {
            Id = "s1",
            Title = "Title",
            Version = "1",
            IsActive = true,
            Questions = new List<CreateQuestionDto>
            {
                new CreateQuestionDto
                {
                    Id = 1,
                    Prompt = "Q1",
                    Choices = new List<CreateChoiceDto>
                    {
                        new CreateChoiceDto { Id = 1, Text = "C1" }
                    }
                }
            }
        };

        _surveyServiceMock.Setup(s => s.SaveSurveyAsync(It.IsAny<Survey>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var result = await _controller.CreateSurveyAsync(dto, CancellationToken.None);

        // Assert
        _surveyServiceMock.Verify();
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal("GetSurveyByIdAsync", createdResult.ActionName);
        Assert.Equal("s1", createdResult.RouteValues?["id"]);
        
        var returnedSurvey = Assert.IsType<Survey>(createdResult.Value);
        Assert.Equal("s1", returnedSurvey.Id);
        Assert.Single(returnedSurvey.Questions);
        Assert.Single(returnedSurvey.Questions[0].Choices);
    }
}
