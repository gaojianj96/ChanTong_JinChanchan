namespace ChanSight.Recorder.Services;

internal sealed class FileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public bool FileExists(string path) => File.Exists(path);

    public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
        => File.WriteAllTextAsync(path, contents, cancellationToken);

    public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
        => File.WriteAllBytesAsync(path, bytes, cancellationToken);

    public Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return File.Exists(path) ? Task.FromResult<string?>(File.ReadAllText(path)) : Task.FromResult<string?>(null);
    }

    public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern)
        => Directory.Exists(directory) ? Directory.EnumerateFiles(directory, searchPattern).ToList() : new List<string>();
}