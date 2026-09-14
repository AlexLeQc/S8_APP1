#nullable enable

using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CoupDeSonde.Core.Storage;

/// <summary>
/// File-based JSON implementation of <see cref="IFileStorage"/>.
/// All reads and writes operate exclusively within the configured <see cref="FileStorageOptions.BaseDirectory"/>.
/// Path traversal attempts are detected and rejected before any I/O is performed.
/// Writes are atomic: data is serialized to a <c>.tmp</c> file and then renamed over the target,
/// preventing partial/corrupted JSON on disk if the process crashes mid-write.
/// </summary>
public sealed class JsonFileStorage : IFileStorage
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly FileStorageOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="JsonFileStorage"/> using the provided options.
    /// </summary>
    /// <param name="options">Storage configuration, including the base directory.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="FileStorageOptions.BaseDirectory"/> is null or whitespace.
    /// </exception>
    public JsonFileStorage(IOptions<FileStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.BaseDirectory))
            throw new ArgumentException(
                "FileStorageOptions.BaseDirectory must be a non-empty string.",
                nameof(options));
    }

    /// <inheritdoc/>
    public async Task<T?> ReadAsync<T>(string relativePath, CancellationToken cancellationToken = default)
    {
        string fullPath = GetSafeFullPath(relativePath);

        if (!File.Exists(fullPath))
            return default;

        await using FileStream stream = File.OpenRead(fullPath);
        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task WriteAsync<T>(string relativePath, T data, CancellationToken cancellationToken = default)
    {
        string fullPath = GetSafeFullPath(relativePath);

        // Ensure the parent directory exists before writing.
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Write to a sibling .tmp file first to guarantee atomicity.
        // If the process crashes or the serialization fails, the original
        // file (if any) is left untouched.
        string tempPath = fullPath + ".tmp";

        try
        {
            await using (FileStream tempStream = new(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(tempStream, data, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);

                // Flush all buffered data to the OS before the rename.
                await tempStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            // Atomic rename: overwrites the destination in a single OS operation.
            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            // Best-effort cleanup of the temp file on failure.
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            throw;
        }
    }

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        string fullPath = GetSafeFullPath(relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        string fullPath = GetSafeFullPath(relativePath);

        // File.Delete is a no-op when the file doesn't exist, which is the
        // desired semantics for this method.
        File.Delete(fullPath);

        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Path Traversal Mitigation (Deadly Sin #10)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Validates and resolves <paramref name="relativePath"/> to an absolute path that is
    /// guaranteed to reside within <see cref="FileStorageOptions.BaseDirectory"/>.
    /// </summary>
    /// <param name="relativePath">The caller-supplied relative path.</param>
    /// <returns>The fully-resolved, safe absolute path.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the path is null/empty, contains null bytes, invalid path characters,
    /// or is an absolute (rooted) path.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the resolved path escapes the configured base directory
    /// (i.e., a path traversal attempt was detected).
    /// </exception>
    private string GetSafeFullPath(string relativePath)
    {
        // 1. Reject null/whitespace inputs.
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Path cannot be null or empty.", nameof(relativePath));

        // 2. Reject null bytes and invalid path characters.
        //    null byte check is explicit because some OS path APIs silently truncate on '\0'.
        if (relativePath.Contains('\0') || relativePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new ArgumentException("Path contains invalid characters.", nameof(relativePath));

        // 3. Reject absolute (rooted) paths — only relative paths are accepted.
        if (Path.IsPathRooted(relativePath))
            throw new ArgumentException("Path must be relative.", nameof(relativePath));

        // 4. Canonicalize the base directory and ensure it ends with the directory separator
        //    so that the prefix check below cannot be fooled by a directory whose name is a
        //    prefix of a sibling (e.g., base = "/data/store", attack = "/data/store2/secret").
        string normalizedBase = Path.GetFullPath(_options.BaseDirectory);
        if (!normalizedBase.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            normalizedBase += Path.DirectorySeparatorChar;

        // 5. Resolve the combined path. Path.GetFullPath collapses ".." sequences, making
        //    traversal attempts visible for the prefix check that follows.
        string fullPath = Path.GetFullPath(Path.Combine(normalizedBase, relativePath));

        // 6. Verify the resolved path is strictly inside the base directory.
        if (!fullPath.StartsWith(normalizedBase, StringComparison.Ordinal))
            throw new UnauthorizedAccessException(
                $"Path traversal attempt detected. Resolved path '{fullPath}' is outside the configured base directory.");

        return fullPath;
    }
}
