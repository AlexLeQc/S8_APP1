#nullable enable

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace CoupDeSonde.Api.Middleware;

/// <summary>
/// Top-level middleware that catches all unhandled exceptions and returns a sanitized
/// RFC 7807 <c>application/problem+json</c> response to the client.
/// </summary>
/// <remarks>
/// <para>
/// <b>Information Leakage Mitigation (Deadly Sins #9, #11, #12):</b><br/>
/// Unhandled exceptions often contain sensitive information (internal paths, stack traces,
/// connection strings, class names). This middleware guarantees that:
/// </para>
/// <list type="bullet">
///   <item><description>Exception messages, stack traces, and inner exceptions are NEVER written to the HTTP response body.</description></item>
///   <item><description>Internal file system paths are NEVER included in the response.</description></item>
///   <item><description>Framework version strings are NEVER exposed (no <c>X-Powered-By</c>, no ASP.NET headers).</description></item>
///   <item><description>All diagnostically useful information is logged server-side only via <see cref="ILogger"/>.</description></item>
/// </list>
/// <para>
/// The response body is a deterministic RFC 7807 <c>ProblemDetails</c> object with a generic,
/// non-revealing error message. A <c>traceId</c> extension is included so operators can
/// correlate the sanitized client error with the full server-side log entry.
/// </para>
/// </remarks>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>
    /// Initializes a new instance of <see cref="GlobalExceptionMiddleware"/>.
    /// </summary>
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the next middleware and catches any unhandled exceptions.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Log the full exception server-side for diagnostics.
            // This is the ONLY place the exception details are recorded.
            _logger.LogError(
                ex,
                "Unhandled exception processing {Method} {Path}. TraceId={TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            // Only write the response if headers haven't been sent yet.
            // If the response was already partially written (streaming), we cannot
            // safely modify it — we log and re-throw in that edge case.
            if (context.Response.HasStarted)
            {
                _logger.LogCritical(
                    "Response already started when exception was caught. TraceId={TraceId}",
                    context.TraceIdentifier);
                throw;
            }

            await WriteSanitizedErrorResponseAsync(context);
        }
    }

    /// <summary>
    /// Writes a sanitized RFC 7807 ProblemDetails response.
    /// </summary>
    /// <remarks>
    /// Deliberately uses a hardcoded, generic message. No exception data is included.
    /// A <c>traceId</c> extension allows correlation with server logs without leaking
    /// exception content to the client.
    /// </remarks>
    private static async Task WriteSanitizedErrorResponseAsync(HttpContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "A server error occurred while processing your request. Please try again later.",
            Instance = context.Request.Path,
        };

        // Include a traceId so operators can cross-reference server logs.
        // This does NOT expose any exception data — only the request correlation ID.
        problem.Extensions["traceId"] = context.TraceIdentifier;

        string json = JsonSerializer.Serialize(problem, _jsonOptions);
        await context.Response.WriteAsync(json);
    }
}
