namespace ChanSight.Core.Annotation;

/// <summary>
/// 带问题列表的加载结果(参考 ReplayLoader.ReplayLoadResult 风格)。
/// 反序列化失败的行进入 <see cref="Issues"/> 而非崩溃。
/// </summary>
public sealed record AnnotationLoadResult<T>
{
    public IReadOnlyList<T> Records { get; init; } = Array.Empty<T>();

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();
}