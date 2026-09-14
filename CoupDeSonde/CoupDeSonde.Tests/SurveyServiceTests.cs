#nullable enable

using CoupDeSonde.Core.Models;
using CoupDeSonde.Core.Services;
using CoupDeSonde.Core.Storage;
using Microsoft.Extensions.Options;

namespace CoupDeSonde.Tests;

/// <summary>
/// Tests for <see cref="SurveyService"/>, exercising every branch of
/// <c>ValidateVoteChoices</c> and the survey persistence round-trip.
/// </summary>
public sealed class SurveyServiceTests : IDisposable
{
    private readonly string _baseDir;
    private readonly SurveyService _sut;

    // A reusable survey with 2 questions, each with 2 choices.
    private static readonly Survey _activeSurvey = new()
    {
        Id = "survey-1",
        Title = "Test Survey",
        Version = "1.0",
        IsActive = true,
        Questions =
        [
            new Question
            {
                Id = 1,
                Prompt = "Q1",
                Choices = [ new Choice { Id = 10, Text = "A" }, new Choice { Id = 11, Text = "B" } ]
            },
            new Question
            {
                Id = 2,
                Prompt = "Q2",
                Choices = [ new Choice { Id = 20, Text = "X" }, new Choice { Id = 21, Text = "Y" } ]
            },
        ]
    };

    public SurveyServiceTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "SurveySvc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_baseDir);

        var storage = new JsonFileStorage(
            Options.Create(new FileStorageOptions { BaseDirectory = _baseDir }));

        _sut = new SurveyService(storage);
    }

    public void Dispose()
    {
        if (Directory.Exists(_baseDir))
            Directory.Delete(_baseDir, recursive: true);
    }

    // ─── Constructor guard ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullStorage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SurveyService(null!));
    }

    // ─── Survey persistence ───────────────────────────────────────────────────

    [Fact]
    public async Task GetSurveyAsync_NonExistentSurvey_ReturnsNull()
    {
        Survey? result = await _sut.GetSurveyAsync("does-not-exist");
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveThenGetSurveyAsync_RoundTripsAllFields()
    {
        await _sut.SaveSurveyAsync(_activeSurvey);
        Survey? loaded = await _sut.GetSurveyAsync("survey-1");

        Assert.NotNull(loaded);
        Assert.Equal("survey-1", loaded.Id);
        Assert.Equal("Test Survey", loaded.Title);
        Assert.Equal("1.0", loaded.Version);
        Assert.True(loaded.IsActive);
        Assert.Equal(2, loaded.Questions.Count);
    }

    [Fact]
    public async Task SaveSurveyAsync_NullSurvey_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.SaveSurveyAsync(null!));
    }

    // ─── ValidateVoteChoices — guard clauses ──────────────────────────────────

    [Fact]
    public void ValidateVoteChoices_NullChoices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _sut.ValidateVoteChoices(_activeSurvey, null!));
    }

    [Fact]
    public void ValidateVoteChoices_NullSurvey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _sut.ValidateVoteChoices(null!, new Dictionary<int, int>()));
    }

    // ─── ValidateVoteChoices — Rule 1: empty submission ───────────────────────

    [Fact]
    public void ValidateVoteChoices_EmptyDictionary_ReturnsFalse()
    {
        bool result = _sut.ValidateVoteChoices(_activeSurvey, new Dictionary<int, int>());
        Assert.False(result);
    }

    // ─── ValidateVoteChoices — Rule 2: count mismatch ─────────────────────────

    [Fact]
    public void ValidateVoteChoices_TooFewAnswers_ReturnsFalse()
    {
        // Only 1 answer for a 2-question survey.
        var choices = new Dictionary<int, int> { [1] = 10 };
        bool result = _sut.ValidateVoteChoices(_activeSurvey, choices);
        Assert.False(result);
    }

    [Fact]
    public void ValidateVoteChoices_TooManyAnswers_ReturnsFalse()
    {
        // 3 answers for a 2-question survey.
        var choices = new Dictionary<int, int> { [1] = 10, [2] = 20, [3] = 99 };
        bool result = _sut.ValidateVoteChoices(_activeSurvey, choices);
        Assert.False(result);
    }

    // ─── ValidateVoteChoices — Rule 3: unknown question ID ───────────────────

    [Fact]
    public void ValidateVoteChoices_UnknownQuestionId_ReturnsFalse()
    {
        // Correct count but a bogus question ID.
        var choices = new Dictionary<int, int> { [1] = 10, [999] = 20 };
        bool result = _sut.ValidateVoteChoices(_activeSurvey, choices);
        Assert.False(result);
    }

    // ─── ValidateVoteChoices — Rule 4: unknown choice ID ─────────────────────

    [Fact]
    public void ValidateVoteChoices_UnknownChoiceId_ReturnsFalse()
    {
        // Valid question IDs, but choice 999 doesn't exist in question 2.
        var choices = new Dictionary<int, int> { [1] = 10, [2] = 999 };
        bool result = _sut.ValidateVoteChoices(_activeSurvey, choices);
        Assert.False(result);
    }

    // ─── ValidateVoteChoices — happy path ────────────────────────────────────

    [Fact]
    public void ValidateVoteChoices_AllValidChoices_ReturnsTrue()
    {
        var choices = new Dictionary<int, int> { [1] = 10, [2] = 21 };
        bool result = _sut.ValidateVoteChoices(_activeSurvey, choices);
        Assert.True(result);
    }

    [Fact]
    public void ValidateVoteChoices_AllSecondChoices_ReturnsTrue()
    {
        var choices = new Dictionary<int, int> { [1] = 11, [2] = 20 };
        bool result = _sut.ValidateVoteChoices(_activeSurvey, choices);
        Assert.True(result);
    }
}
