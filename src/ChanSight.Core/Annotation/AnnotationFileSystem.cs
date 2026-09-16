using System.Text;

namespace ChanSight.Core.Annotation;

internal sealed class AnnotationFileSystem : IFileSystem
{
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public async Task AppendLineAsync(string path, string line, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.AppendAllTextAsync(fullPath, line + "\n", Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    public Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return File.Exists(path) ? Task.FromResult<string?>(File.ReadAllText(path)) : Task.FromResult<string?>(null);
    }
}