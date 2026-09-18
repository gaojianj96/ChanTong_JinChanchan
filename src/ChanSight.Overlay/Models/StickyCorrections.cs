using ChanSight.Core.Annotation;

namespace ChanSight.Overlay.Models;

/// <summary>
/// 一条粘滞修正: 记录对某格(RegionType + CellIndex)的人工修正值, 以及作出修正时
/// 该格的识别值(<see cref="Baseline"/>)。当后续识别值实质变化(换英雄/升星)即失效。
/// </summary>
public sealed record StickyEntry(string RegionType, int CellIndex, string? Baseline, string Corrected)
{
    public static string MakeKey(string regionType, int cellIndex) => $"{regionType}:{cellIndex}";

    public string Key => MakeKey(RegionType, CellIndex);
}

/// <summary>
/// 格粒度粘滞修正表(纯逻辑)。维护格子 → 修正值; 若该格后续识别值相对于
/// <see cref="StickyEntry.Baseline"/> 实质变化则自动失效(移除)。
/// </summary>
public sealed class StickyCorrections
{
    private readonly Dictionary<string, StickyEntry> _entries = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, StickyEntry> Entries => _entries;

    public void Set(StickyEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries[entry.Key] = entry;
    }

    public StickyEntry? Get(string regionType, int cellIndex) =>
        _entries.TryGetValue(StickyEntry.MakeKey(regionType, cellIndex), out var entry) ? entry : null;

    /// <summary>
    /// 用该格的最新识别值刷新粘滞状态。识别值相对于 baseline 实质变化则失效并返回 false,
    /// 否则保持生效并返回 true(表示应继续显示修正值)。
    /// </summary>
    public bool IsSticky(string regionType, int cellIndex, string? recognizedValue)
    {
        var entry = Get(regionType, cellIndex);
        if (entry is null)
        {
            return false;
        }

        if (!SubstantiallyEqual(entry.Baseline, recognizedValue))
        {
            Invalidate(regionType, cellIndex);
            return false;
        }

        return true;
    }

    public bool Invalidate(string regionType, int cellIndex) =>
        _entries.Remove(StickyEntry.MakeKey(regionType, cellIndex));

    public void Clear() => _entries.Clear();

    /// <summary>识别值比较: 空(null/空白)视为相等; 其余用 ordinal(大小写不敏感)字符串比较。</summary>
    private static bool SubstantiallyEqual(string? a, string? b)
    {
        var aEmpty = string.IsNullOrWhiteSpace(a);
        var bEmpty = string.IsNullOrWhiteSpace(b);
        if (aEmpty || bEmpty)
        {
            return aEmpty && bEmpty;
        }

        return string.Equals(a!.Trim(), b!.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 修正值的 JSON 载体构造助手: 统一生成 <see cref="CorrectionRecord.CorrectedValue"/> 的合法 JSON 字符串。
/// </summary>
public static class CorrectionValue
{
    public static string Hero(string name) => System.Text.Json.JsonSerializer.Serialize(new { hero = name });

    public static string Star(int star) => System.Text.Json.JsonSerializer.Serialize(new { star });

    public static string Items(IReadOnlyList<string> items) =>
        System.Text.Json.JsonSerializer.Serialize(new { items });

    public static string Empty() => System.Text.Json.JsonSerializer.Serialize(new { empty = true });
}