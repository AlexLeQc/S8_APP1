using CoupDeSonde.Api.Configuration;
using CoupDeSonde.Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CoupDeSonde.Tests;

public class ApiKeyAuthenticationFilterTests
{
    private readonly Mock<ILogger<ApiKeyAuthenticationFilter>> _mockLogger = new();
    private readonly ApiKeyOptions _options = new()
    {
        HeaderName = "X-Api-Key",
        ValidKeys = new List<string> { "valid-key-1", "valid-key-2" }
    };

    private ApiKeyAuthenticationFilter CreateFilter()
    {
        var optionsMock = new Mock<IOptions<ApiKeyOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_options);
        return new ApiKeyAuthenticationFilter(optionsMock.Object, _mockLogger.Object);
    }

    private ActionExecutingContext CreateContext(string? headerValue)
    {
        var httpContext = new DefaultHttpContext();
        if (headerValue != null)
        {
            httpContext.Request.Headers[_options.HeaderName] = headerValue;
        }

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor()
        );

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object()
        );
    }

    [Fact]
    public async Task OnActionExecutionAsync_MissingHeader_Returns401()
    {
        // Arrange
        var filter = CreateFilter();
        var context = CreateContext(null);
        var nextCalled = false;
        Task<ActionExecutedContext> Next() { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); }

        // Act
        await filter.OnActionExecutionAsync(context, Next);

        // Assert
        Assert.False(nextCalled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("API key is missing.", problem.Detail);
    }

    [Fact]
    public async Task OnActionExecutionAsync_EmptyHeader_Returns401()
    {
        // Arrange
        var filter = CreateFilter();
        var context = CreateContext("   ");
        var nextCalled = false;
        Task<ActionExecutedContext> Next() { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); }

        // Act
        await filter.OnActionExecutionAsync(context, Next);

        // Assert
        Assert.False(nextCalled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task OnActionExecutionAsync_InvalidKey_Returns401()
    {
        // Arrange
        var filter = CreateFilter();
        var context = CreateContext("invalid-key");
        var nextCalled = false;
        Task<ActionExecutedContext> Next() { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); }

        // Act
        await filter.OnActionExecutionAsync(context, Next);

        // Assert
        Assert.False(nextCalled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Invalid API key.", problem.Detail);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ValidKey_CallsNext()
    {
        // Arrange
        var filter = CreateFilter();
        var context = CreateContext("valid-key-2");
        var nextCalled = false;
        Task<ActionExecutedContext> Next() { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); }

        // Act
        await filter.OnActionExecutionAsync(context, Next);

        // Assert
        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }
}
