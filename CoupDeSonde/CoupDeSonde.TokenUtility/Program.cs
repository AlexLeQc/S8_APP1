#nullable enable

using CoupDeSonde.Core.Security;
using CoupDeSonde.Core.Storage;
using Microsoft.Extensions.Options;

// ─────────────────────────────────────────────────────────────────────────────
// CoupDeSonde Token Utility
// Offline CLI tool — generates single-use ballot tokens for a given survey.
// Plaintext tokens are written to a local file for distribution (e.g. email).
// Hash records are persisted in the same data directory as the running API.
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine("╔══════════════════════════════════════════════╗");
Console.WriteLine("║      CoupDeSonde — Token Generation Utility  ║");
Console.WriteLine("╚══════════════════════════════════════════════╝");
Console.WriteLine();

// ── 1. Locate the API data directory ─────────────────────────────────────────
//
// Default: relative to the utility binary so it works when run from the
// solution root with `dotnet run --project CoupDeSonde.TokenUtility`.
// Override by setting the COUPSONDE_DATA_DIR environment variable.

string defaultDataDir = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                 "CoupDeSonde.Api", "data"));

string dataDir = Environment.GetEnvironmentVariable("COUPSONDE_DATA_DIR")
                 ?? defaultDataDir;

Console.WriteLine($"  Data directory : {dataDir}");

if (!Directory.Exists(dataDir))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"[ERROR] Data directory not found: {dataDir}");
    Console.Error.WriteLine("        Set the COUPSONDE_DATA_DIR environment variable to the correct path.");
    Console.ResetColor();
    return 1;
}

// ── 2. Gather inputs ──────────────────────────────────────────────────────────

Console.Write("  Survey ID      : ");
string? surveyId = Console.ReadLine()?.Trim();

if (string.IsNullOrWhiteSpace(surveyId))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine("[ERROR] Survey ID must not be empty.");
    Console.ResetColor();
    return 1;
}

// Verify the survey JSON file exists in the data directory.
string surveyFile = Path.Combine(dataDir, "surveys", $"{surveyId}.json");
if (!File.Exists(surveyFile))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Error.WriteLine($"[WARN] Survey file not found: {surveyFile}");
    Console.Error.WriteLine("       Tokens will still be generated but they may not be usable.");
    Console.ResetColor();
}

Console.Write("  Number of tokens: ");
string? countInput = Console.ReadLine()?.Trim();

if (!int.TryParse(countInput, out int tokenCount) || tokenCount <= 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine("[ERROR] Number of tokens must be a positive integer.");
    Console.ResetColor();
    return 1;
}

Console.WriteLine();
Console.WriteLine($"  Generating {tokenCount} token(s) for survey '{surveyId}' …");
Console.WriteLine();

// ── 3. Instantiate Core services ──────────────────────────────────────────────

var storageOptions = new FileStorageOptions { BaseDirectory = dataDir };
IFileStorage storage = new JsonFileStorage(Options.Create(storageOptions));
ITokenService tokenService = new TokenService(storage);

// ── 4. Generate tokens ────────────────────────────────────────────────────────

var plaintextTokens = new List<string>(tokenCount);

using var cts = new CancellationTokenSource();

// Allow Ctrl+C to abort mid-generation cleanly.
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine();
    Console.WriteLine("[ABORT] Generation cancelled by user.");
};

try
{
    for (int i = 0; i < tokenCount; i++)
    {
        (string plaintext, var ballot) = await tokenService
            .IssueBallotTokenAsync(surveyId, cts.Token)
            .ConfigureAwait(false);

        plaintextTokens.Add(plaintext);

        Console.WriteLine($"  [{i + 1,4}/{tokenCount}] hash={ballot.TokenHash[..16]}…  stored ✓");
    }
}
catch (OperationCanceledException)
{
    // User pressed Ctrl+C — write whatever was generated so far.
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"[WARN] Generation interrupted after {plaintextTokens.Count} token(s).");
    Console.ResetColor();
}

if (plaintextTokens.Count == 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine("[ERROR] No tokens were generated.");
    Console.ResetColor();
    return 1;
}

// ── 5. Write plaintext tokens to output file ──────────────────────────────────
//
// The output file is created in the current working directory.
// It simulates the distribution list (e.g. to be emailed to voters).
// IMPORTANT: this file contains sensitive plaintext tokens — handle accordingly.

string outputFileName = $"generated_tokens_{surveyId}.txt";
string outputPath = Path.Combine(Directory.GetCurrentDirectory(), outputFileName);

var lines = new List<string>
{
    $"# CoupDeSonde — Single-Use Ballot Tokens",
    $"# Survey  : {surveyId}",
    $"# Generated: {DateTimeOffset.UtcNow:O}",
    $"# Count   : {plaintextTokens.Count}",
    $"#",
    $"# ⚠  CONFIDENTIAL — each token is single-use and must be sent to exactly one voter.",
    $"#    Delete this file after distribution.",
    string.Empty,
};

lines.AddRange(plaintextTokens);

await File.WriteAllLinesAsync(outputPath, lines, cts.Token).ConfigureAwait(false);

// ── 6. Summary ────────────────────────────────────────────────────────────────

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"✔  {plaintextTokens.Count} token(s) generated successfully.");
Console.ResetColor();
Console.WriteLine($"   Output file : {outputPath}");
Console.WriteLine();
Console.WriteLine("   Distribute one token per voter. Tokens are single-use.");
Console.WriteLine("   The API data directory now contains the hashed records.");
Console.WriteLine();

return 0;
