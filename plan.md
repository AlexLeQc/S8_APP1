# Phase 2: Core Domain Logic & Anonymous Token Generation

Implementation of the domain models, file-based JSON persistence abstraction, cryptographic token services, survey validation, and anonymous voting logic within `CoupDeSonde.Core`, meeting all security requirements from `gei771.pdf` and 100% branch testability.

## User Review Required

> [!IMPORTANT]
> **Key Security Architecture Decisions:**
>
> 1. **Cryptographic Randomness (Deadly Sin #20):** Tokens are generated via `System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)` and encoded as Base64Url string (256 bits entropy). Tokens are **never** stored in plain text; only their SHA-256 hashes (`System.Security.Cryptography.SHA256.HashData`) are persisted.
> 2. **Path Traversal Mitigation (Deadly Sin #10):** `JsonFileStorage` resolves relative paths strictly under a designated root directory using `Path.GetFullPath`. Any path resolving outside this root, containing traversal sequences (`..`), null bytes, or invalid path characters will be rejected with an `ArgumentException` or `SecurityException`.
> 3. **Race Condition & Token Replay Prevention (Deadly Sin #13):** `IVotingService` enforces atomic single-use consumption of ballot tokens using localized asynchronous concurrency synchronization (e.g. semaphore/file lock per token hash) before persisting the anonymous vote and updating `IsConsumed = true`.
> 4. **Voter Anonymity (Secret Ballot):** The stored vote records (`VoteRecord`) do **not** contain the ballot token or token hash, preventing correlation between voter identity and cast ballots.

---

## Proposed Architecture & Class Specifications

### 1. Domain Models (`CoupDeSonde.Core/Models/`)

- [NEW] [Choice.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Models/Choice.cs)
  - `int Id`: Unique ID of the choice within the question.
  - `string Text`: The option label/text (must not be null or empty).
- [NEW] [Question.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Models/Question.cs)
  - `int Id`: Unique question identifier.
  - `string Prompt`: The question prompt/text.
  - `List<Choice> Choices`: Collection of available choices (must have at least one choice).
- [NEW] [Survey.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Models/Survey.cs)
  - `string Id`: Survey identifier (validated for alphanumeric/safe chars).
  - `string Title`: Survey title.
  - `string Version`: Survey version string.
  - `List<Question> Questions`: Ordered questions.
  - `bool IsActive`: Flag indicating whether the survey accepts responses.
- [NEW] [BallotToken.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Models/BallotToken.cs)
  - `string TokenHash`: SHA-256 hex string of the single-use token.
  - `string SurveyId`: Survey to which this ballot is tied.
  - `DateTimeOffset CreatedAt`: Timestamp of creation.
  - `bool IsConsumed`: Single-use status flag.
  - `DateTimeOffset? ConsumedAt`: Nullable timestamp when the vote was registered.
- [NEW] [VoteSubmission.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Models/VoteSubmission.cs)
  - `string SurveyId`: Identifier of the target survey.
  - `Dictionary<int, int> QuestionChoices`: Map of `QuestionId` -> `ChoiceId`.
  - `string BallotToken`: Raw plaintext cryptographic token provided by the voter.
- [NEW] [VoteRecord.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Models/VoteRecord.cs)
  - `string Id`: Unique GUID for the persisted vote.
  - `string SurveyId`: Associated survey ID.
  - `Dictionary<int, int> QuestionChoices`: Anonymized question-choice selections.
  - `DateTimeOffset Timestamp`: Submission timestamp.

---

### 2. Storage Abstraction (`CoupDeSonde.Core/Storage/`)

- [NEW] [IFileStorage.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Storage/IFileStorage.cs)
  ```csharp
  public interface IFileStorage
  {
      Task<T?> ReadAsync<T>(string relativePath, CancellationToken cancellationToken = default);
      Task WriteAsync<T>(string relativePath, T data, CancellationToken cancellationToken = default);
      Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default);
      Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
  }
  ```
- [NEW] [FileStorageOptions.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Storage/FileStorageOptions.cs)
  - Configuration holding `BaseDirectory`.
- [NEW] [JsonFileStorage.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Storage/JsonFileStorage.cs)
  - **Path Validation Logic**:

    ```csharp
    private string GetSafeFullPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Path cannot be null or empty.", nameof(relativePath));
        if (relativePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || relativePath.Contains('\0'))
            throw new ArgumentException("Path contains invalid characters.", nameof(relativePath));

        // Prevent absolute path escapes
        if (Path.IsPathRooted(relativePath))
            throw new ArgumentException("Path must be relative.", nameof(relativePath));

        string normalizedBase = Path.GetFullPath(_options.BaseDirectory);
        if (!normalizedBase.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            normalizedBase += Path.DirectorySeparatorChar;
        }

        string fullPath = Path.GetFullPath(Path.Combine(normalizedBase, relativePath));

        if (!fullPath.StartsWith(normalizedBase, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Path traversal attempt detected.");
        }

        return fullPath;
    }
    ```

  - Atomic file writing using temporary files (`.tmp`) and `File.Move(..., overwrite: true)` to avoid partial or corrupted JSON on disk.
  - Serializes with `System.Text.Json` using camelCase and indented formatting.

---

### 3. Cryptographic Token Service (`CoupDeSonde.Core/Security/`)

- [NEW] [ITokenService.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Security/ITokenService.cs)
  ```csharp
  public interface ITokenService
  {
      string GenerateToken();
      string HashToken(string token);
      Task<(string PlaintextToken, BallotToken Ballot)> IssueBallotTokenAsync(string surveyId, CancellationToken cancellationToken = default);
  }
  ```
- [NEW] [TokenService.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Security/TokenService.cs)
  - `GenerateToken()`: Generates 32 bytes with `RandomNumberGenerator.GetBytes(32)` and returns `Convert.ToHexString(bytes).ToLowerInvariant()` (or Base64Url).
  - `HashToken(string token)`: Computes `SHA256.HashData(Encoding.UTF8.GetBytes(token))` and formats as hex string.
  - `IssueBallotTokenAsync(surveyId)`: Generates raw token, hashes it, constructs `BallotToken`, persists it via `IFileStorage` under `tokens/{surveyId}/{tokenHash}.json`, and returns both the plaintext token (to be sent once to voter) and the stored `BallotToken` entity.

---

### 4. Survey & Voting Services (`CoupDeSonde.Core/Services/`)

- [NEW] [ISurveyService.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Services/ISurveyService.cs)
  ```csharp
  public interface ISurveyService
  {
      Task<Survey?> GetSurveyAsync(string surveyId, CancellationToken cancellationToken = default);
      Task SaveSurveyAsync(Survey survey, CancellationToken cancellationToken = default);
      bool ValidateVoteChoices(Survey survey, IReadOnlyDictionary<int, int> questionChoices);
  }
  ```
- [NEW] [SurveyService.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Services/SurveyService.cs)
  - Stores surveys under `surveys/{surveyId}.json`.
  - `ValidateVoteChoices`:
    - Checks if `questionChoices` is null or empty.
    - Verifies that the number of answered questions matches the survey's active questions.
    - Validates that every QuestionId exists in the survey.
    - Validates that each selected ChoiceId exists in the question's `Choices`.

- [NEW] [IVotingService.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Services/IVotingService.cs)
  ```csharp
  public interface IVotingService
  {
      Task<VoteResult> SubmitVoteAsync(VoteSubmission submission, CancellationToken cancellationToken = default);
  }
  ```
- [NEW] [VoteResult.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Services/VoteResult.cs)
  - Result type indicating success or specific failure reason:
    - `Success`, `SurveyNotFound`, `SurveyInactive`, `InvalidChoices`, `InvalidToken`, `TokenAlreadyConsumed`, `InvalidSubmission`.
- [NEW] [VotingService.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Services/VotingService.cs)
  - Validates non-null input parameters.
  - Fetches `Survey` via `ISurveyService`; verifies active status.
  - Validates choices via `ISurveyService.ValidateVoteChoices`.
  - Hashes `submission.BallotToken` via `ITokenService.HashToken`.
  - Concurrency guard: utilizes an in-memory lock/semaphore keyed by token hash (or storage lock) to eliminate race conditions (Sin #13).
  - Loads `BallotToken` from storage:
    - If null -> returns `InvalidToken`.
    - If `IsConsumed` -> returns `TokenAlreadyConsumed`.
  - Marks token as consumed (`IsConsumed = true`, `ConsumedAt = DateTimeOffset.UtcNow`), saves token to storage.
  - Persists anonymous `VoteRecord` under `votes/{surveyId}/{Guid.NewGuid()}.json`.

---

## Proposed Changes

### Component: CoupDeSonde.Core

#### [DELETE] [Class1.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Class1.cs)

- Remove boilerplate template file.

#### [NEW] [Choice.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Models/Choice.cs)

#### [NEW] [Question.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Models/Question.cs)

#### [NEW] [Survey.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Models/Survey.cs)

#### [NEW] [BallotToken.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Models/BallotToken.cs)

#### [NEW] [VoteSubmission.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Models/VoteSubmission.cs)

#### [NEW] [VoteRecord.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Models/VoteRecord.cs)

#### [NEW] [IFileStorage.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Storage/IFileStorage.cs)

#### [NEW] [FileStorageOptions.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Storage/FileStorageOptions.cs)

#### [NEW] [JsonFileStorage.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Storage/JsonFileStorage.cs)

#### [NEW] [ITokenService.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Security/ITokenService.cs)

#### [NEW] [TokenService.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Security/TokenService.cs)

#### [NEW] [ISurveyService.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Services/ISurveyService.cs)

#### [NEW] [SurveyService.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Services/SurveyService.cs)

#### [NEW] [IVotingService.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Services/IVotingService.cs)

#### [NEW] [VoteResult.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Services/VoteResult.cs)

#### [NEW] [VotingService.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Core/Services/VotingService.cs)

---

### Component: CoupDeSonde.Tests

- [NEW] [JsonFileStorageTests.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Tests/JsonFileStorageTests.cs)
  - Test path traversal attacks: `../../etc/passwd`, `C:\Windows`, absolute paths, `..\\..\\secret.json`, null characters (`\0`), whitespace paths.
  - Test normal write, read, exists, delete, and overwrite.
- [NEW] [TokenServiceTests.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Tests/TokenServiceTests.cs)
  - Verify high entropy (no collisions across 1,000 generated tokens).
  - Verify deterministic SHA-256 output.
  - Verify token issuance and storage hashing.
- [NEW] [SurveyServiceTests.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Tests/SurveyServiceTests.cs)
  - Test validation of choice IDs matching questions.
  - Test invalid question IDs, invalid choice IDs, missing answers, null submissions.
- [NEW] [VotingServiceTests.cs](file:///Users/alexisguerard/Projects/APP1/CoupDeSonde/CoupDeSonde.Tests/VotingServiceTests.cs)
  - Test valid voting workflow with anonymous persistence.
  - Test single-use token consumption rejection (replay attack).
  - Test inactive survey rejection.
  - Test concurrent voting attempts using identical token (race condition / double voting).

---

## Verification Plan

### Automated Tests

Execute dotnet test suite with coverage collection:

```bash
dotnet test CoupDeSonde/CoupDeSonde.Tests/CoupDeSonde.Tests.csproj --collect:"XPlat Code Coverage"
```

Verify:

1. All unit tests pass.
2. 100% branch coverage achieved across core security branches (path validation, token verification, choice matching).
