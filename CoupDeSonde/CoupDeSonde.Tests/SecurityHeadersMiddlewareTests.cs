using CoupDeSonde.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace CoupDeSonde.Tests;

public class SecurityHeadersMiddlewareTests
{
    private class TestHttpResponseFeature : HttpResponseFeature
    {
        private Func<object, Task>? _callback;
        private object? _state;

        public override void OnStarting(Func<object, Task> callback, object state)
        {
            _callback = callback;
            _state = state;
        }

        public Task InvokeStartingAsync()
        {
            if (_callback != null)
            {
                return _callback(_state!);
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task InvokeAsync_InjectsAllSecurityHeaders()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var responseFeature = new TestHttpResponseFeature();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        
        bool nextCalled = false;
        var middleware = new SecurityHeadersMiddleware(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.Headers["Server"] = "Kestrel";

        // Act
        await middleware.InvokeAsync(context);
        await responseFeature.InvokeStartingAsync();

        // Assert
        Assert.True(nextCalled);
        
        var headers = context.Response.Headers;
        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.Equal("default-src 'none'; frame-ancestors 'none'; form-action 'none'", headers["Content-Security-Policy"]);
        Assert.Equal("max-age=31536000; includeSubDomains", headers["Strict-Transport-Security"]);
        Assert.Equal("no-referrer", headers["Referrer-Policy"]);
        Assert.Equal("0", headers["X-XSS-Protection"]);
        Assert.Equal("camera=(), microphone=(), geolocation=(), payment=()", headers["Permissions-Policy"]);
        
        Assert.False(headers.ContainsKey("Server"));
    }
}
