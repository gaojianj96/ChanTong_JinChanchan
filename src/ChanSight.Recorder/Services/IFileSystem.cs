namespace ChanSight.Recorder.Services;

internal interface IFileSystem
{
    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    bool FileExists(string path);

    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);

    Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default);

    Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern);
}