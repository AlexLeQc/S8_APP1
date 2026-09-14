#nullable enable

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoupDeSonde.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace CoupDeSonde.Api.Filters;

/// <summary>
/// An action filter that enforces API key authentication on decorated endpoints.
/// </summary>
/// <remarks>
/// <para>
/// <b>Timing Attack Mitigation (Deadly Sins #12 &amp; #21):</b>
/// Rather than comparing the raw key strings directly (which leaks key length via early exit),
/// both the incoming key and each configured valid key are first hashed with SHA-256 to produce
/// fixed-length 32-byte digests. Comparison is then performed exclusively via
/// <see cref="CryptographicOperations.FixedTimeEquals"/>, which executes in constant time
/// regardless of where the first differing byte occurs.
/// </para>
/// <para>
/// The loop always iterates over ALL configured keys even after a match is found,
/// preventing the number of valid keys from leaking through response timing.
/// </para>
/// </remarks>
public sealed class ApiKeyAuthenticationFilter : IAsyncActionFilter
{
    private readonly ApiKeyOptions _options;
    private readonly ILogger<ApiKeyAuthenticationFilter> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Initializes a new instance of <see cref="ApiKeyAuthenticationFilter"/>.
    /// </summary>
    public ApiKeyAuthenticationFilter(
        IOptions<ApiKeyOptions> options,
        ILogger<ApiKeyAuthenticationFilter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Extract the API key header value.
        if (!context.HttpContext.Request.Headers.TryGetValue(_options.HeaderName, out var headerValues)
            || string.IsNullOrWhiteSpace(headerValues.FirstOrDefault()))
        {
            _logger.LogWarning(
                "API key authentication failed: header '{Header}' is missing. RemoteIP={IP}",
                _options.HeaderName,
                context.HttpContext.Connection.RemoteIpAddress);

            context.Result = BuildUnauthorizedResult("API key is missing.", context.HttpContext.Request.Path);
            return;
        }

        string providedKey = headerValues.First()!;

        // Hash the incoming key once to a fixed 32-byte digest.
        byte[] providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));

        // Always iterate ALL configured keys to prevent timing leakage via early exit.
        // This prevents an attacker from deducing the number of valid keys through response time.
        bool isAuthenticated = false;
        foreach (string validKey in _options.ValidKeys)
        {
            byte[] validHash = SHA256.HashData(Encoding.UTF8.GetBytes(validKey));

            // Fixed-time comparison: executes in constant time regardless of where
            // the first byte difference occurs. Eliminates side-channel timing leaks.
            if (CryptographicOperations.FixedTimeEquals(providedHash, validHash))
            {
                isAuthenticated = true;
                // Do NOT break — continue iterating to prevent timing attacks
                // based on position of the matching key in the list.
            }
        }

        if (!isAuthenticated)
        {
            _logger.LogWarning(
                "API key authentication failed: invalid key presented. RemoteIP={IP}",
                context.HttpContext.Connection.RemoteIpAddress);

            context.Result = BuildUnauthorizedResult("Invalid API key.", context.HttpContext.Request.Path);
            return;
        }

        _logger.LogDebug("API key authentication succeeded. RemoteIP={IP}", context.HttpContext.Connection.RemoteIpAddress);
        await next();
    }

    /// <summary>
    /// Constructs a sanitized <see cref="ObjectResult"/> with RFC 7807 <see cref="ProblemDetails"/>
    /// for a 401 Unauthorized response. Does not expose internal configuration or key values.
    /// </summary>
    private static ObjectResult BuildUnauthorizedResult(string detail, string instance)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
            Title = "Unauthorized",
            Status = StatusCodes.Status401Unauthorized,
            Detail = detail,
            Instance = instance,
        };

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            ContentTypes = { "application/problem+json" },
        };
    }
}
