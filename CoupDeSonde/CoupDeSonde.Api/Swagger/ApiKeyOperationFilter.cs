#nullable enable

using CoupDeSonde.Api.Filters;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CoupDeSonde.Api.Swagger;

/// <summary>
/// A Swashbuckle <see cref="IOperationFilter"/> that automatically annotates OpenAPI operations
/// requiring API key authentication with the correct security requirement and 401 response.
/// </summary>
/// <remarks>
/// <para>
/// This filter inspects the action's endpoint metadata for the presence of
/// <see cref="ApiKeyAuthorizeAttribute"/>. When found, it:
/// </para>
/// <list type="number">
///   <item><description>Adds an <c>ApiKey</c> security requirement to the OpenAPI operation, linking it to the global <c>ApiKey</c> security scheme definition.</description></item>
///   <item><description>Adds a <c>401 Unauthorized</c> response descriptor so consumers of the OpenAPI spec know which endpoints require authentication.</description></item>
/// </list>
/// <para>
/// The global security scheme (<c>ApiKey</c>, in-header, name <c>X-Api-Key</c>) must be registered
/// separately in <c>AddSwaggerGen</c> via <c>AddSecurityDefinition</c>.
/// </para>
/// </remarks>
public sealed class ApiKeyOperationFilter : IOperationFilter
{
    /// <inheritdoc/>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Check whether this action or its controller is decorated with [ApiKeyAuthorize].
        bool requiresApiKey = context.MethodInfo
            .GetCustomAttributes(typeof(ApiKeyAuthorizeAttribute), inherit: true)
            .Any()
            || (context.MethodInfo.DeclaringType?
                .GetCustomAttributes(typeof(ApiKeyAuthorizeAttribute), inherit: true)
                .Any() ?? false);

        if (!requiresApiKey)
        {
            return;
        }

        // Register a 401 Unauthorized response for this operation in the spec.
        if (operation.Responses is not null)
        {
            operation.Responses.TryAdd("401", new OpenApiResponse
            {
                Description = "Unauthorized — the X-Api-Key header is missing or invalid.",
            });
        }

        // Use OpenApiSecuritySchemeReference (Microsoft.OpenApi 2.x API — OpenApiReference was removed).
        // This references the globally-defined "ApiKey" scheme registered via AddSecurityDefinition.
        var securitySchemeReference = new OpenApiSecuritySchemeReference("ApiKey");

        // Add the security requirement to this specific operation.
        // OpenApiSecurityRequirement values are List<string> in Microsoft.OpenApi 2.x.
        if (operation.Security is not null)
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [securitySchemeReference] = new List<string>(),
            });
        }
    }
}
