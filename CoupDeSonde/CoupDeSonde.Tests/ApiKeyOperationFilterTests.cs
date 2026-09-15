#nullable enable

using CoupDeSonde.Api.Filters;
using CoupDeSonde.Api.Swagger;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace CoupDeSonde.Tests;

public class ApiKeyOperationFilterTests
{
    private class DummyController
    {
        [ApiKeyAuthorize]
        public void ProtectedAction() { }

        public void PublicAction() { }
    }

    [ApiKeyAuthorize]
    private class ProtectedController
    {
        public void InheritedAction() { }
    }

    private static OperationFilterContext MakeContext(System.Reflection.MethodInfo methodInfo)
        => new OperationFilterContext(
            apiDescription: new Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription(),
            schemaRegistry: null!,
            schemaRepository: new SchemaRepository(),
            document: new OpenApiDocument
            {
                Components = new OpenApiComponents
                {
                    SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                    {
                        ["ApiKey"] = new OpenApiSecurityScheme
                        {
                            Type = SecuritySchemeType.ApiKey,
                            Name = "X-Api-Key",
                            In = ParameterLocation.Header
                        }
                    }
                }
            },
            methodInfo: methodInfo);

    [Fact]
    public void Apply_WhenProtected_AddsSecurityRequirementAnd401Response()
    {
        var filter = new ApiKeyOperationFilter();
        var operation = new OpenApiOperation();
        var context = MakeContext(typeof(DummyController).GetMethod(nameof(DummyController.ProtectedAction))!);

        filter.Apply(operation, context);

        Assert.NotNull(operation.Responses);
        Assert.True(operation.Responses.ContainsKey("401"));
        Assert.NotNull(operation.Security);
        Assert.Single(operation.Security);
    }

    [Fact]
    public void Apply_WhenClassProtected_AddsSecurityRequirement()
    {
        var filter = new ApiKeyOperationFilter();
        var operation = new OpenApiOperation();
        var context = MakeContext(typeof(ProtectedController).GetMethod(nameof(ProtectedController.InheritedAction))!);

        filter.Apply(operation, context);

        Assert.NotNull(operation.Responses);
        Assert.True(operation.Responses.ContainsKey("401"));
        Assert.NotNull(operation.Security);
        Assert.Single(operation.Security);
    }

    [Fact]
    public void Apply_WhenPublic_DoesNotAddSecurity()
    {
        var filter = new ApiKeyOperationFilter();
        var operation = new OpenApiOperation();
        var context = MakeContext(typeof(DummyController).GetMethod(nameof(DummyController.PublicAction))!);

        filter.Apply(operation, context);

        Assert.False(operation.Responses?.ContainsKey("401") ?? false);
        Assert.Null(operation.Security);
    }
}
