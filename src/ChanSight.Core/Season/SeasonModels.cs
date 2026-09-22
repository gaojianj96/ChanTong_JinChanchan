namespace ChanSight.Core.Season;

/// <summary>
/// 赛季字典的数据模型(纯数据, 被 Vision/Overlay 复用, 不依赖 OpenCvSharp)。
/// </summary>

/// <summary>按 season + mode 唯一标识一套字典/上下文的复合键。</summary>
public sealed record SeasonModeKey(string SeasonId, string Mode);

/// <summary>识别/推荐管线中透传的显式赛季上下文。</summary>
public sealed record SeasonContext(string SeasonId, string Mode);

/// <summary>某个赛季的正式字典(mode 无关), 集合默认空。</summary>
public sealed record SeasonDictionary(
    string SeasonId,
    IReadOnlySet<string> Heroes,
    IReadOnlySet<string> Items,
    IReadOnlySet<string> Traits)
{
    /// <summary>
    /// 英雄名→费用(1-5)。用 init 属性 + 默认空字典, 避免破坏既有 4 位置参数的构造点。
    /// </summary>
    public IReadOnlyDictionary<string, int> HeroCosts { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>人工确认一条自学习候选时的处置语义。</summary>
public enum ConfirmKind
{
    /// <summary>候选映射到已有正式条目(记 alias), 不新增正式条目。</summary>
    MapExisting,

    /// <summary>作为新条目写入正式字典。</summary>
    New,

    /// <summary>忽略该候选, 不写入正式字典。</summary>
    Ignore,
}

/// <summary>自学习候选的元数据: 对局中发现、尚未进入正式字典。</summary>
public sealed record CandidateMeta(
    string Entity,
    string Source,
    string? Url,
    double Confidence,
    DateTimeOffset FirstSeen);