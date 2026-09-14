#nullable enable

namespace CoupDeSonde.Core.Storage;

/// <summary>
/// Abstraction for a file-based key-value store that serializes objects as JSON files
/// under a configured base directory. All paths are relative to that base directory.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Deserializes a JSON file at the given relative path into an instance of <typeparamref name="T"/>.
    /// Returns <c>null</c> if the file does not exist.
    /// </summary>
    /// <typeparam name="T">The type to deserialize into.</typeparam>
    /// <param name="relativePath">A relative path within the storage base directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized object, or <c>null</c> if the file does not exist.</returns>
    Task<T?> ReadAsync<T>(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes <paramref name="data"/> and atomically writes it to the given relative path.
    /// Parent directories are created automatically.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <param name="relativePath">A relative path within the storage base directory.</param>
    /// <param name="data">The data to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteAsync<T>(string relativePath, T data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a file exists at the given relative path.
    /// </summary>
    /// <param name="relativePath">A relative path within the storage base directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the file exists; otherwise <c>false</c>.</returns>
    Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the file at the given relative path.
    /// Does nothing if the file does not exist.
    /// </summary>
    /// <param name="relativePath">A relative path within the storage base directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}
