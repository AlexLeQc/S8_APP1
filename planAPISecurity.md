# Implementation Plan: CoupDeSonde.Api HTTP Layer & Security Controls

This plan details the implementation of the HTTP API layer, security middleware, filters, and OpenAPI specifications for **CoupDeSonde.Api** in .NET 10. It addresses the security directives of `gei771.pdf` and the _24 Deadly Sins of Software Security_ (David LeBlanc & Michael Howard), particularly around authentication timing attacks, information leakage, error sanitization, secure headers, and participant anonymity.

---

## User Review Required

> [!IMPORTANT]
> **Key Security Architecture Decisions:**
>
> 1. **Timing Attack Elimination on API Key Verification (Deadly Sin #21 & #12):**
>    To prevent side-channel timing attacks on API key verification, `ApiKeyAuthenticationFilter` will pre-hash both the incoming header and configured keys using `SHA256.HashData`, then execute `CryptographicOperations.FixedTimeEquals` on the fixed-length (32-byte) byte spans. This eliminates both character-by-character comparison leaks and length-disclosure leaks.
> 2. **Information Disclosure Prevention via ProblemDetails (Deadly Sins #9, #11, #12):**
>    `GlobalExceptionMiddleware` catches all unhandled exceptions, logs internal details securely via `ILogger`, and emits a sanitized RFC 7807 `application/problem+json` payload. It strictly guarantees that internal server file paths, stack traces, exception messages, and framework versions are never leaked to HTTP clients.
> 3. **Defense-in-Depth HTTP Security Headers:**
>    `SecurityHeadersMiddleware` ensures every outbound HTTP response includes strict protective headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`, and `Strict-Transport-Security: max-age=31536000; includeSubDomains`).
> 4. **API Versioning Scheme:**
>    URL path versioning `/api/v{version:apiVersion}/...` (e.g. `/api/v1/surveys`, `/api/v1/tokens`, `/api/v1/votes`) configured via `Asp.Versioning.Mvc`.

---

## Architecture Overview & File Structure

```
CoupDeSonde.Api/
├── Configuration/
│   └── ApiKeyOptions.cs                     [NEW] Configuration binding for X-Api-Key authentication
├── Filters/
│   ├── ApiKeyAuthenticationFilter.cs        [NEW] IAsyncActionFilter using CryptographicOperations.FixedTimeEquals
│   └── ApiKeyAuthorizeAttribute.cs          [NEW] Attribute for securing admin endpoints
├── Middleware/
│   ├── SecurityHeadersMiddleware.cs         [NEW] Injects nosniff, DENY, CSP, HSTS headers
│   └── GlobalExceptionMiddleware.cs         [NEW] Catches unhandled exceptions, returns sanitized ProblemDetails
├── DTOs/
│   ├── CreateSurveyRequestDto.cs            [NEW] Input model for creating surveys (admin)
│   ├── SurveySummaryDto.cs                  [NEW] Output model for public survey listings
│   ├── TokenRequestDto.cs                   [NEW] Input model for voter ballot token requests
│   ├── TokenResponseDto.cs                  [NEW] Output model containing the single-use plaintext token
│   └── SubmitVoteRequestDto.cs              [NEW] Input model for ballot submissions
├── Controllers/
│   ├── SurveysController.cs                 [NEW] /api/v1/surveys: GET list/detail, POST (Admin API Key)
│   ├── TokensController.cs                  [NEW] /api/v1/tokens: POST (issue ballot token)
│   └── VotesController.cs                   [NEW] /api/v1/votes: POST (submit vote)
├── Swagger/
│   └── ApiKeyOperationFilter.cs             [NEW] Swashbuckle operation filter to annotate secured endpoints
├── Program.cs                               [MODIFY] Registers services, middleware pipeline, versioning & OpenAPI
├── appsettings.json                         [MODIFY] Configuration entries for FileStorage & ApiKeys
└── CoupDeSonde.Api.csproj                   [MODIFY] Ensure required package references
```

---

## Detailed Component Specifications

### 1. Configuration (`CoupDeSonde.Api/Configuration/`)

#### [NEW] [ApiKeyOptions.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Configuration/ApiKeyOptions.cs)

- Binds to `appsettings.json` section `"Authentication:ApiKeys"`.
- Properties:
  - `public string HeaderName { get; set; } = "X-Api-Key";`
  - `public List<string> ValidKeys { get; set; } = new();`

---

### 2. Filters & Authentication (`CoupDeSonde.Api/Filters/`)

#### [NEW] [ApiKeyAuthenticationFilter.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Filters/ApiKeyAuthenticationFilter.cs)

- Implements `IAsyncActionFilter`.
- Injected with `IOptions<ApiKeyOptions>` and `ILogger<ApiKeyAuthenticationFilter>`.
- **Timing Attack Mitigation (Deadly Sin #21 & #12)**:
  1. Reads header `X-Api-Key` from `context.HttpContext.Request.Headers`.
  2. If missing or empty: returns `401 Unauthorized` with a standardized `ProblemDetails` object (`Title = "Unauthorized"`, `Detail = "API key is missing."`).
  3. Pre-hashes the incoming key using `SHA256.HashData(Encoding.UTF8.GetBytes(providedKey))` to produce a fixed 32-byte digest.
  4. Compares the hash against the pre-hashed configured valid keys using:
     ```csharp
     byte[] providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
     bool isAuthenticated = false;
     foreach (var validKey in _options.ValidKeys)
     {
         byte[] validHash = SHA256.HashData(Encoding.UTF8.GetBytes(validKey));
         if (CryptographicOperations.FixedTimeEquals(providedHash, validHash))
         {
             isAuthenticated = true;
         }
     }
     ```
  5. If invalid: returns `401 Unauthorized` with `ProblemDetails` (`Detail = "Invalid API key."`).
  6. If valid: invokes `await next()`.

#### [NEW] [ApiKeyAuthorizeAttribute.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Filters/ApiKeyAuthorizeAttribute.cs)

- Custom `[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]`.
- Inherits from `TypeFilterAttribute` with `typeof(ApiKeyAuthenticationFilter)` for convenient annotation of admin-only endpoints.

---

### 3. Middlewares (`CoupDeSonde.Api/Middleware/`)

#### [NEW] [SecurityHeadersMiddleware.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Middleware/SecurityHeadersMiddleware.cs)

- Injected in HTTP pipeline before routing and response generation.
- Appends security headers to `context.Response.Headers`:
  - `X-Content-Type-Options: nosniff` (prevents MIME type sniffing)
  - `X-Frame-Options: DENY` (prevents framing and clickjacking attacks)
  - `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` (restricts all resource loading for pure API endpoints)
  - `Strict-Transport-Security: max-age=31536000; includeSubDomains` (enforces HTTPS - HSTS per `gei771.pdf` Deliverable #1)
  - `Referrer-Policy: no-referrer`
  - `X-XSS-Protection: 0` (disables deprecated and buggy browser XSS filters in modern browsers)

#### [NEW] [GlobalExceptionMiddleware.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Middleware/GlobalExceptionMiddleware.cs)

- Wraps request pipeline execution in a `try/catch`.
- **Information Leakage & Error Handling Mitigation (Deadly Sins #9, #11, #12)**:
  - Logs unhandled exceptions with full details (stack trace, exception type) via `ILogger<GlobalExceptionMiddleware>` for diagnostics.
  - Sanitizes the client response:
    - Sets `context.Response.StatusCode = StatusCodes.Status500InternalServerError`.
    - Sets `context.Response.ContentType = "application/problem+json"`.
    - Returns serialized RFC 7807 `ProblemDetails`:
      ```json
      {
        "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        "title": "An unexpected error occurred.",
        "status": 500,
        "detail": "A server error occurred while processing your request. Please try again later.",
        "instance": "/api/v1/surveys",
        "traceId": "0HN123456789"
      }
      ```
  - Never leaks exception messages, file paths, or stack traces in HTTP responses.

---

### 4. DTOs (`CoupDeSonde.Api/DTOs/`)

#### [NEW] [CreateSurveyRequestDto.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/DTOs/CreateSurveyRequestDto.cs)

- Input model for admin survey creation.
- Fields: `Id` (string), `Title` (string), `Version` (string), `Questions` (List of `Question` models), `IsActive` (bool).
- Includes validation rules ensuring non-empty title, safe alphanumeric ID, and valid questions/choices.

#### [NEW] [SurveySummaryDto.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/DTOs/SurveySummaryDto.cs)

- Lightweight model for survey listing: `Id`, `Title`, `Version`, `IsActive`, `QuestionCount`.

#### [NEW] [TokenRequestDto.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/DTOs/TokenRequestDto.cs)

- Voter eligibility request: `SurveyId` (string), `VoterIdentifier` (string).

#### [NEW] [TokenResponseDto.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/DTOs/TokenResponseDto.cs)

- Single-use plaintext token response returned once to voter: `BallotToken` (string), `SurveyId` (string), `CreatedAt` (DateTimeOffset).

#### [NEW] [SubmitVoteRequestDto.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/DTOs/SubmitVoteRequestDto.cs)

- Submission model: `SurveyId` (string), `BallotToken` (string), `QuestionChoices` (Dictionary<int, int>).

---

### 5. Controllers (`CoupDeSonde.Api/Controllers/`)

All controllers are decorated with:

```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
```

#### [NEW] [SurveysController.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Controllers/SurveysController.cs)

- Route: `/api/v1/surveys`
- Endpoints:
  - `GET /api/v1/surveys`: Public. Retrieves all existing surveys from storage. Returns `200 OK` with list of `SurveySummaryDto`.
  - `GET /api/v1/surveys/{id}`: Public. Retrieves specific survey details. Returns `200 OK` with `Survey`, or `404 Not Found` with `ProblemDetails`.
  - `POST /api/v1/surveys`: **Admin Only** (decorated with `[ApiKeyAuthorize]`). Accepts `CreateSurveyRequestDto`. Validates input, saves survey via `ISurveyService.SaveSurveyAsync`. Returns `201 Created` with route to `GET /api/v1/surveys/{id}`.

#### [NEW] [TokensController.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Controllers/TokensController.cs)

- Route: `/api/v1/tokens`
- Endpoints:
  - `POST /api/v1/tokens`: Public (or voter-facing). Accepts `TokenRequestDto`.
    1. Verifies that the requested `SurveyId` exists and is active via `ISurveyService.GetSurveyAsync`.
    2. If survey not found: `404 Not Found` (`ProblemDetails`).
    3. If survey is inactive: `400 Bad Request` (`ProblemDetails: "Survey is not accepting votes."`).
    4. Calls `ITokenService.IssueBallotTokenAsync(surveyId)`.
    5. Returns `201 Created` with `TokenResponseDto` containing the raw plaintext `BallotToken` (transmitted once to voter; token hash stored on disk per Dead Sin #20).

#### [NEW] [VotesController.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Controllers/VotesController.cs)

- Route: `/api/v1/votes`
- Endpoints:
  - `POST /api/v1/votes`: Public. Accepts `SubmitVoteRequestDto`.
    1. Maps DTO to `VoteSubmission`.
    2. Calls `IVotingService.SubmitVoteAsync(submission)`.
    3. Maps `VoteResult` enum to HTTP responses:
       - `VoteResult.Success` -> `200 OK` (`{ "message": "Vote cast successfully." }`).
       - `VoteResult.SurveyNotFound` -> `404 Not Found` (`ProblemDetails`).
       - `VoteResult.SurveyInactive` -> `400 Bad Request` (`ProblemDetails: "Survey is inactive."`).
       - `VoteResult.InvalidChoices` -> `400 Bad Request` (`ProblemDetails: "Invalid question or choice selections."`).
       - `VoteResult.InvalidSubmission` -> `400 Bad Request` (`ProblemDetails: "Invalid vote payload."`).
       - `VoteResult.InvalidToken` -> `401 Unauthorized` (`ProblemDetails: "Ballot token is invalid."`).
       - `VoteResult.TokenAlreadyConsumed` -> `409 Conflict` (`ProblemDetails: "Ballot token has already been used (replay attack prevented)."`).

---

### 6. OpenAPI & Swagger (`CoupDeSonde.Api/Swagger/`)

#### [NEW] [ApiKeyOperationFilter.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Swagger/ApiKeyOperationFilter.cs)

- Inspects endpoint metadata for `ApiKeyAuthorizeAttribute`.
- Adds `ApiKey` security requirement and `401 Unauthorized` response to the OpenAPI operation specification.

#### Swagger Configuration in `Program.cs`

- Configures Swashbuckle with `ApiKey` scheme:

  ```csharp
  builder.Services.AddSwaggerGen(c =>
  {
      c.SwaggerDoc("v1", new OpenApiInfo
      {
          Title = "CoupDeSonde API",
          Version = "v1",
          Description = "Secure anonymous voting and survey API - GEI771"
      });

      c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
      {
          Description = "Admin API Key header. Example: 'X-Api-Key: secret123'",
          Name = "X-Api-Key",
          In = ParameterLocation.Header,
          Type = SecuritySchemeType.ApiKey,
          Scheme = "ApiKey"
      });

      c.OperationFilter<ApiKeyOperationFilter>();
  });
  ```

- Exposes `/swagger/v1/swagger.json` and Swagger UI at `/swagger` where testers can authorize requests directly using `X-Api-Key`.

---

### 7. Dependency Injection & Pipeline (`Program.cs`)

#### [MODIFY] [Program.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Program.cs)

- Register Core services:
  - `builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("FileStorage"));`
  - `builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection("Authentication:ApiKeys"));`
  - `builder.Services.AddSingleton<IFileStorage, JsonFileStorage>();`
  - `builder.Services.AddScoped<ITokenService, TokenService>();`
  - `builder.Services.AddScoped<ISurveyService, SurveyService>();`
  - `builder.Services.AddScoped<IVotingService, VotingService>();`
  - `builder.Services.AddScoped<ApiKeyAuthenticationFilter>();`
- Register Versioning:
  ```csharp
  builder.Services.AddApiVersioning(options =>
  {
      options.DefaultApiVersion = new ApiVersion(1, 0);
      options.AssumeDefaultVersionWhenUnspecified = true;
      options.ReportApiVersions = true;
      options.ApiVersionReader = new UrlSegmentApiVersionReader();
  }).AddMvc();
  ```
- Pipeline ordering:
  1. `app.UseMiddleware<GlobalExceptionMiddleware>();` (top level catch-all)
  2. `app.UseMiddleware<SecurityHeadersMiddleware>();` (security headers on all responses)
  3. `app.UseHttpsRedirection();` (HTTPS mandatory per Deliverable #1)
  4. `app.UseHsts();` (when not in development)
  5. `app.UseSwagger(); app.UseSwaggerUI();`
  6. `app.UseRouting();`
  7. `app.MapControllers();`

---

## Proposed Changes Summary

### Component: CoupDeSonde.Api

#### [MODIFY] [CoupDeSonde.Api.csproj](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/CoupDeSonde.Api.csproj)

- Ensure all necessary package references (`Asp.Versioning.Mvc`, `Swashbuckle.AspNetCore`, `Microsoft.AspNetCore.OpenApi`).

#### [MODIFY] [appsettings.json](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/appsettings.json)

- Add `"FileStorage": { "BaseDirectory": "data" }`.
- Add `"Authentication": { "ApiKeys": { "HeaderName": "X-Api-Key", "ValidKeys": ["dev-admin-key-coupsonde-2026"] } }`.

#### [MODIFY] [Program.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Program.cs)

- Full service registration and middleware setup.

#### [NEW] [ApiKeyOptions.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Configuration/ApiKeyOptions.cs)

#### [NEW] [ApiKeyAuthenticationFilter.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Filters/ApiKeyAuthenticationFilter.cs)

#### [NEW] [ApiKeyAuthorizeAttribute.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Filters/ApiKeyAuthorizeAttribute.cs)

#### [NEW] [SecurityHeadersMiddleware.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Middleware/SecurityHeadersMiddleware.cs)

#### [NEW] [GlobalExceptionMiddleware.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Middleware/GlobalExceptionMiddleware.cs)

#### [NEW] [CreateSurveyRequestDto.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/DTOs/CreateSurveyRequestDto.cs)

#### [NEW] [SurveySummaryDto.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/DTOs/SurveySummaryDto.cs)

#### [NEW] [TokenRequestDto.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/DTOs/TokenRequestDto.cs)

#### [NEW] [TokenResponseDto.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/DTOs/TokenResponseDto.cs)

#### [NEW] [SubmitVoteRequestDto.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/DTOs/SubmitVoteRequestDto.cs)

#### [NEW] [SurveysController.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Controllers/SurveysController.cs)

#### [NEW] [TokensController.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Controllers/TokensController.cs)

#### [NEW] [VotesController.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Controllers/VotesController.cs)

#### [NEW] [ApiKeyOperationFilter.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Api/Swagger/ApiKeyOperationFilter.cs)

---

### Component: CoupDeSonde.Tests

#### [NEW] [ApiKeyAuthenticationFilterTests.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Tests/ApiKeyAuthenticationFilterTests.cs)

- Valid API key permits execution.
- Missing API key returns 401 with `ProblemDetails`.
- Invalid API key returns 401 with `ProblemDetails`.
- Timing equality: verified constant-time comparison path.

#### [NEW] [SecurityHeadersMiddlewareTests.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Tests/SecurityHeadersMiddlewareTests.cs)

- Verifies that all expected headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Content-Security-Policy`, `Strict-Transport-Security`) are present on HTTP responses.

#### [NEW] [GlobalExceptionMiddlewareTests.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Tests/GlobalExceptionMiddlewareTests.cs)

- Simulates unhandled exception in downstream middleware.
- Verifies response is 500 `application/problem+json`.
- Verifies that response body does NOT contain exception message, stack trace, or server paths.

#### [NEW] [SurveysControllerTests.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Tests/SurveysControllerTests.cs)

- Unit tests for GET all surveys, GET by ID (found / not found).
- POST survey validation (valid survey returns 201, invalid returns 400).

#### [NEW] [TokensControllerTests.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Tests/TokensControllerTests.cs)

- Requests token for valid active survey returns 201 with token string.
- Requests token for non-existent survey returns 404.
- Requests token for inactive survey returns 400.

#### [NEW] [VotesControllerTests.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Tests/VotesControllerTests.cs)

- Tests all 7 outcomes of `VoteResult` mapped to correct HTTP status codes (200, 400, 401, 404, 409).

---

## Verification Plan

### Automated Tests

1. Run the full xUnit test suite (combining Core and API tests):
   ```bash
   dotnet test CoupDeSonde/CoupDeSonde.Tests/CoupDeSonde.Tests.csproj --collect:"XPlat Code Coverage"
   ```
2. Verify that all existing 81 tests plus new API unit tests pass with 0 failures.
3. Validate code coverage report targeting comprehensive branch coverage across all new filters, middleware, and controller paths.

### Manual Verification

- Launch the API using `dotnet run --project CoupDeSonde/CoupDeSonde.Api`
- Navigate to `/swagger` to confirm the OpenAPI spec renders and the `Authorize` dialog with `X-Api-Key` operates as designed.
