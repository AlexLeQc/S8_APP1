using System.Text.Json;
using CoupDeSonde.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoupDeSonde.Tests;

public class GlobalExceptionMiddlewareTests
{
    private readonly Mock<ILogger<GlobalExceptionMiddleware>> _loggerMock = new();

    [Fact]
    public async Task InvokeAsync_NoException_CallsNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        bool nextCalled = false;
        var middleware = new GlobalExceptionMiddleware(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ThrowsException_ResponseNotStarted_ReturnsProblemDetails()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/test-path";
        context.TraceIdentifier = "test-trace-id";

        var originalBodyStream = new MemoryStream();
        context.Response.Body = originalBodyStream;

        var middleware = new GlobalExceptionMiddleware(ctx =>
        {
            throw new InvalidOperationException("Test exception");
        }, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        originalBodyStream.Position = 0;
        using var reader = new StreamReader(originalBodyStream);
        var responseBody = await reader.ReadToEndAsync();

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var problem = JsonSerializer.Deserialize<ProblemDetails>(responseBody, jsonOptions);

        Assert.NotNull(problem);
        Assert.Equal("An unexpected error occurred.", problem!.Title);
        Assert.Equal("A server error occurred while processing your request. Please try again later.", problem.Detail);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
        Assert.Equal("/test-path", problem.Instance);
        
        Assert.True(problem.Extensions.ContainsKey("traceId"));
        Assert.Equal("test-trace-id", problem.Extensions["traceId"]?.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ThrowsException_ResponseHasStarted_Rethrows()
    {
        // Arrange
        var context = new DefaultHttpContext();
        // Simulate response already started (DefaultHttpContext allows setting this if we use a mock feature or similar, 
        // but DefaultHttpContext's HasStarted is false by default. We can mock IHttpResponseFeature).
        var responseFeatureMock = new Mock<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>();
        responseFeatureMock.Setup(f => f.HasStarted).Returns(true);
        context.Features.Set(responseFeatureMock.Object);

        var middleware = new GlobalExceptionMiddleware(ctx =>
        {
            throw new InvalidOperationException("Test exception");
        }, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }
}
