namespace ChanSight.Core.Annotation;

/// <summary>
/// Annotation 存储所需的最小文件系统抽象。
/// 定义于 Core 内以避免 Core → Recorder 的反向依赖(Recorder 的 IFileSystem 为 internal)。
/// </summary>
public interface IFileSystem
{
    void CreateDirectory(string path);

    Task AppendLineAsync(string path, string line, CancellationToken cancellationToken = default);

    Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
}