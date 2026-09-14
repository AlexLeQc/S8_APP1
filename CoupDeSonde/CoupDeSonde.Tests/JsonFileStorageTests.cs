#nullable enable

using CoupDeSonde.Core.Storage;
using Microsoft.Extensions.Options;

namespace CoupDeSonde.Tests;

/// <summary>
/// Tests for <see cref="JsonFileStorage"/>.
/// Covers every branch of <c>GetSafeFullPath</c> (path traversal mitigation)
/// and the full CRUD contract (write, read, exists, delete, overwrite).
/// Each test method runs inside a fresh temporary directory that is cleaned
/// up automatically via <see cref="IDisposable"/>.
/// </summary>
public sealed class JsonFileStorageTests : IDisposable
{
    private readonly string _baseDir;
    private readonly JsonFileStorage _sut;

    public JsonFileStorageTests()
    {
        // Each test class instance gets its own isolated temp directory.
        _baseDir = Path.Combine(Path.GetTempPath(), "CoupDeSonde_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_baseDir);

        _sut = CreateStorage(_baseDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_baseDir))
            Directory.Delete(_baseDir, recursive: true);
    }

    // ─── Constructor guards ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonFileStorage(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyBaseDirectory_ThrowsArgumentException(string baseDir)
    {
        var opts = Options.Create(new FileStorageOptions { BaseDirectory = baseDir });
        Assert.Throws<ArgumentException>(() => new JsonFileStorage(opts));
    }

    // ─── Path traversal: null / whitespace ───────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WriteAsync_NullOrWhitespacePath_ThrowsArgumentException(string path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.WriteAsync(path, new { value = 1 }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReadAsync_NullOrWhitespacePath_ThrowsArgumentException(string path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ReadAsync<object>(path));
    }

    // ─── Path traversal: null byte ────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_PathWithNullByte_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.WriteAsync("file\0name.json", new { }));
    }

    [Fact]
    public async Task ReadAsync_PathWithNullByte_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ReadAsync<object>("file\0name.json"));
    }

    // ─── Path traversal: absolute (rooted) paths ─────────────────────────────

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("/tmp/secret.json")]
    public async Task WriteAsync_AbsolutePath_ThrowsArgumentException(string path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.WriteAsync(path, new { }));
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("/tmp/secret.json")]
    public async Task ReadAsync_AbsolutePath_ThrowsArgumentException(string path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ReadAsync<object>(path));
    }

    // ─── Path traversal: ".." sequences ──────────────────────────────────────

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("../secret.json")]
    [InlineData("subfolder/../../secret.json")]
    public async Task WriteAsync_TraversalSequence_ThrowsUnauthorizedAccessException(string path)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.WriteAsync(path, new { }));
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("../secret.json")]
    [InlineData("subfolder/../../secret.json")]
    public async Task ReadAsync_TraversalSequence_ThrowsUnauthorizedAccessException(string path)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.ReadAsync<object>(path));
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    public async Task ExistsAsync_TraversalSequence_ThrowsUnauthorizedAccessException(string path)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.ExistsAsync(path));
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    public async Task DeleteAsync_TraversalSequence_ThrowsUnauthorizedAccessException(string path)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.DeleteAsync(path));
    }

    // ─── Normal CRUD operations ───────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_ThenReadAsync_RoundTripsData()
    {
        var payload = new TestPayload { Name = "Alice", Score = 42 };

        await _sut.WriteAsync("data/alice.json", payload);
        TestPayload? result = await _sut.ReadAsync<TestPayload>("data/alice.json");

        Assert.NotNull(result);
        Assert.Equal("Alice", result.Name);
        Assert.Equal(42, result.Score);
    }

    [Fact]
    public async Task ReadAsync_NonExistentFile_ReturnsNull()
    {
        TestPayload? result = await _sut.ReadAsync<TestPayload>("does/not/exist.json");
        Assert.Null(result);
    }

    [Fact]
    public async Task ExistsAsync_AfterWrite_ReturnsTrue()
    {
        await _sut.WriteAsync("survey.json", new { id = "s1" });
        bool exists = await _sut.ExistsAsync("survey.json");
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_MissingFile_ReturnsFalse()
    {
        bool exists = await _sut.ExistsAsync("missing.json");
        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteAsync_ExistingFile_FileIsRemoved()
    {
        await _sut.WriteAsync("toDelete.json", new { });
        await _sut.DeleteAsync("toDelete.json");
        bool exists = await _sut.ExistsAsync("toDelete.json");
        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentFile_DoesNotThrow()
    {
        // Should be a no-op — no exception expected.
        await _sut.DeleteAsync("ghost.json");
    }

    [Fact]
    public async Task WriteAsync_NestedPath_CreatesParentDirectories()
    {
        await _sut.WriteAsync("deep/nested/dir/file.json", new TestPayload { Name = "Bob", Score = 7 });

        string expectedPath = Path.Combine(_baseDir, "deep", "nested", "dir", "file.json");
        Assert.True(File.Exists(expectedPath));
    }

    // ─── Atomic overwrite behaviour ───────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_CalledTwice_OverwritesFile()
    {
        await _sut.WriteAsync("counter.json", new TestPayload { Name = "v1", Score = 1 });
        await _sut.WriteAsync("counter.json", new TestPayload { Name = "v2", Score = 2 });

        TestPayload? result = await _sut.ReadAsync<TestPayload>("counter.json");

        Assert.NotNull(result);
        Assert.Equal("v2", result.Name);
        Assert.Equal(2, result.Score);
    }

    [Fact]
    public async Task WriteAsync_LeavesNoTempFile_OnSuccess()
    {
        await _sut.WriteAsync("clean.json", new TestPayload { Name = "x", Score = 0 });

        string tmpPath = Path.Combine(_baseDir, "clean.json.tmp");
        Assert.False(File.Exists(tmpPath), "Temp file should be renamed/removed after a successful write.");
    }

    // ─── Sibling-directory prefix spoofing guard ──────────────────────────────

    [Fact]
    public async Task WriteAsync_SiblingDirectoryNamePrefix_ThrowsUnauthorizedAccessException()
    {
        // The base dir is e.g. /tmp/CoupDeSonde_Tests_<guid>
        // Attack: attempt to escape to /tmp/CoupDeSonde_Tests_<guid>_evil
        // Path.GetFullPath would resolve the path OUTSIDE the base dir.
        // Construct the attack relative to the parent of _baseDir.
        string parentDir = Path.GetDirectoryName(_baseDir)!;
        string siblingName = Path.GetFileName(_baseDir) + "_evil";
        string attackRelative = Path.Combine("..", siblingName, "secret.json");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.WriteAsync(attackRelative, new { }));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static JsonFileStorage CreateStorage(string baseDir) =>
        new(Options.Create(new FileStorageOptions { BaseDirectory = baseDir }));

    private sealed class TestPayload
    {
        public string Name { get; set; } = string.Empty;
        public int Score { get; set; }
    }
}
