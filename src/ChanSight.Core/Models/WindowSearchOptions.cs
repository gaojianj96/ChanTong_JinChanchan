namespace ChanSight.Core.Models;

public sealed record WindowSearchOptions
{
    public static IReadOnlyList<string> DefaultTitleKeywords { get; } =
    [
        "金铲铲之战",
        "MuMu",
        "雷电",
        "夜神",
        "Tencent"
    ];

    public nint? Hwnd { get; init; }

    public int? ProcessId { get; init; }

    public IReadOnlyCollection<string> TitleKeywords { get; init; } = DefaultTitleKeywords;

    public bool IncludeMinimized { get; init; }
}
