#nullable enable

namespace CoupDeSonde.Core.Storage;

/// <summary>
/// Configuration options for <see cref="JsonFileStorage"/>.
/// </summary>
public sealed class FileStorageOptions
{
    /// <summary>
    /// Gets or sets the absolute path to the root directory under which all JSON files are stored.
    /// Every path passed to <see cref="IFileStorage"/> is resolved relative to this directory.
    /// The directory must not be null or whitespace.
    /// </summary>
    public string BaseDirectory { get; set; } = string.Empty;
}
