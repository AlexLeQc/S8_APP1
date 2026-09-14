#nullable enable

using System.Security.Cryptography;
using System.Text;
using CoupDeSonde.Core.Models;
using CoupDeSonde.Core.Storage;

namespace CoupDeSonde.Core.Security;

/// <summary>
/// Cryptographic implementation of <see cref="ITokenService"/>.
/// Uses <see cref="RandomNumberGenerator"/> (CSPRNG) for token generation and
/// <see cref="SHA256"/> for deterministic, one-way hashing.
/// Plaintext tokens are ephemeral — they exist only long enough to be returned
/// to the caller and are never written to disk.
/// </summary>
public sealed class TokenService : ITokenService
{
    // Token storage path template: tokens/{surveyId}/{tokenHash}.json
    private const string TokenPathTemplate = "tokens/{0}/{1}.json";

    private readonly IFileStorage _storage;

    /// <summary>
    /// Initializes a new instance of <see cref="TokenService"/>.
    /// </summary>
    /// <param name="storage">The file storage used to persist ballot token records.</param>
    public TokenService(IFileStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        _storage = storage;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Generates 32 bytes (256 bits) of cryptographically-secure random data via
    /// <see cref="RandomNumberGenerator.GetBytes(int)"/> (Deadly Sin #20 mitigation).
    /// The bytes are encoded as a lowercase hexadecimal string for safe transmission.
    /// </remarks>
    public string GenerateToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Uses <see cref="SHA256.HashData(byte[])"/> — a one-way function — so the
    /// original plaintext token cannot be recovered from the stored hash.
    /// </remarks>
    public string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token, nameof(token));

        byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
        byte[] hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The issuance flow:
    /// <list type="number">
    ///   <item>Generate a 256-bit CSPRNG token (plaintext).</item>
    ///   <item>Hash the plaintext with SHA-256 to obtain the storage key.</item>
    ///   <item>Construct a <see cref="BallotToken"/> record (no plaintext stored).</item>
    ///   <item>Persist the record under <c>tokens/{surveyId}/{tokenHash}.json</c>.</item>
    ///   <item>Return both values; the plaintext is sent once to the voter.</item>
    /// </list>
    /// </remarks>
    public async Task<(string PlaintextToken, BallotToken Ballot)> IssueBallotTokenAsync(
        string surveyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surveyId, nameof(surveyId));

        string plaintextToken = GenerateToken();
        string tokenHash = HashToken(plaintextToken);

        BallotToken ballot = new()
        {
            TokenHash = tokenHash,
            SurveyId = surveyId,
            CreatedAt = DateTimeOffset.UtcNow,
            IsConsumed = false,
            ConsumedAt = null,
        };

        string path = string.Format(TokenPathTemplate, surveyId, tokenHash);
        await _storage.WriteAsync(path, ballot, cancellationToken).ConfigureAwait(false);

        return (plaintextToken, ballot);
    }
}
