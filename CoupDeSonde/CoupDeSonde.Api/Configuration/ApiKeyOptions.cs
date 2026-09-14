#nullable enable

namespace CoupDeSonde.Api.Configuration;

/// <summary>
/// Configuration options for API key authentication.
/// Bound from the <c>Authentication:ApiKeys</c> section in <c>appsettings.json</c>.
/// </summary>
public sealed class ApiKeyOptions
{
    /// <summary>
    /// The HTTP request header name used to transmit the API key.
    /// Defaults to <c>X-Api-Key</c>.
    /// </summary>
    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>
    /// The list of valid plaintext API keys. These are compared using
    /// timing-safe hashing (<see cref="System.Security.Cryptography.CryptographicOperations.FixedTimeEquals"/>)
    /// to prevent side-channel timing attacks.
    /// </summary>
    public List<string> ValidKeys { get; set; } = new();
}
