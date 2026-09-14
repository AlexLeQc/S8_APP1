using Asp.Versioning;
using CoupDeSonde.Api.Configuration;
using CoupDeSonde.Api.Filters;
using CoupDeSonde.Api.Middleware;
using CoupDeSonde.Api.Swagger;
using CoupDeSonde.Core.Security;
using CoupDeSonde.Core.Services;
using CoupDeSonde.Core.Storage;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// 1. Bind Configuration Options
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("FileStorage"));
builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection("Authentication:ApiKeys"));

// 2. Register Core Services
builder.Services.AddSingleton<IFileStorage, JsonFileStorage>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ISurveyService, SurveyService>();
builder.Services.AddScoped<IVotingService, VotingService>();

// 3. Register Filters
builder.Services.AddScoped<ApiKeyAuthenticationFilter>();

// 4. Add Controllers
builder.Services.AddControllers();

// 5. Add API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'V";
    options.SubstituteApiVersionInUrl = true;
});

// 6. Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CoupDeSonde API",
        Version = "v1",
        Description = "Secure anonymous voting and survey API - GEI771"
    });

    // Define the ApiKey security scheme
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Admin API Key header. Example: 'X-Api-Key: secret123'",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKey"
    });

    // Apply the operation filter for endpoints decorated with [ApiKeyAuthorize]
    c.OperationFilter<ApiKeyOperationFilter>();
});

var app = builder.Build();

// ==========================================
// Middleware Pipeline (Order is critical)
// ==========================================

// A. Catch all unhandled exceptions and sanitize error responses (Deadly Sins #9, #11, #12)
app.UseMiddleware<GlobalExceptionMiddleware>();

// B. Inject strict security headers on every response
app.UseMiddleware<SecurityHeadersMiddleware>();

// C. Enforce HTTPS
app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
{
    // Enable HSTS (Strict-Transport-Security) in non-dev environments
    app.UseHsts();
}

// D. Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CoupDeSonde API v1");
});

// E. Routing & Controllers
app.UseRouting();
app.MapControllers();

app.Run();
