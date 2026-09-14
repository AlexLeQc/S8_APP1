#nullable enable

using Microsoft.AspNetCore.Mvc;

namespace CoupDeSonde.Api.Filters;

/// <summary>
/// Marks a controller action (or entire controller) as requiring API key authentication.
/// Resolved from the DI container via <see cref="TypeFilterAttribute"/> so that
/// <see cref="ApiKeyAuthenticationFilter"/> receives its injected dependencies.
/// </summary>
/// <remarks>
/// Usage example:
/// <code>
/// [ApiKeyAuthorize]
/// [HttpPost]
/// public async Task&lt;IActionResult&gt; CreateSurvey([FromBody] CreateSurveyRequestDto dto) { ... }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ApiKeyAuthorizeAttribute : TypeFilterAttribute
{
    /// <summary>
    /// Initializes the attribute, instructing ASP.NET Core to instantiate
    /// <see cref="ApiKeyAuthenticationFilter"/> from the DI container.
    /// </summary>
    public ApiKeyAuthorizeAttribute() : base(typeof(ApiKeyAuthenticationFilter))
    {
    }
}
