using System.Text.Json;

namespace ChanSight.Core.Evaluation;

/// <summary>
/// 识别值 vs 金标答案的语义等价比较器(纯逻辑, 无语义依赖)。
/// 支持 <see cref="ChanSight.Core.Annotation.GoldLabel.CorrectedValue"/> 的 JSON 结构化比较:
/// 双侧共同出现的 hero / star / items 任一字段不同即判不等;
/// 空值(null / 空白 / <c>{"empty":true}</c>)彼此语义等价。
/// </summary>
public static class CorrectionValue
{
    /// <summary>
    /// 语义等价判定。仅比较双侧共同出现的字段(识别值通常只携带英雄名,
    /// 故单侧缺失的 star/items 不参与比较)。
    /// </summary>
    public static bool Equals(string? recognized, string? corrected)
    {
        var r = SemanticValue.Parse(recognized);
        var c = SemanticValue.Parse(corrected);

        if (r.IsEmpty || c.IsEmpty)
        {
            return r.IsEmpty == c.IsEmpty;
        }

        if (r.Hero is not null
            && c.Hero is not null
            && !string.Equals(r.Hero.Trim(), c.Hero.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (r.Star.HasValue && c.Star.HasValue && r.Star.Value != c.Star.Value)
        {
            return false;
        }

        if (r.Items is not null && c.Items is not null && !SameItems(r.Items, c.Items))
        {
            return false;
        }

        return true;
    }

    /// <summary>值是否表示"空"(null / 空白 / <c>{"empty":true}</c>)。</summary>
    internal static bool IsEmpty(string? value) => SemanticValue.Parse(value).IsEmpty;

    private static bool SameItems(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        return a
            .Select(static x => x.Trim())
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(
                b.Select(static x => x.Trim()).OrderBy(static x => x, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
    }

    private readonly record struct SemanticValue(bool IsEmpty, string? Hero, int? Star, IReadOnlyList<string>? Items)
    {
        public static SemanticValue Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new SemanticValue(true, null, null, null);
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(value);
            }
            catch (JsonException)
            {
                // 纯文本识别值 → 视为英雄名。
                return new SemanticValue(false, value.Trim(), null, null);
            }

            using (doc)
            {
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Null)
                {
                    return new SemanticValue(true, null, null, null);
                }

                if (root.ValueKind == JsonValueKind.String)
                {
                    return new SemanticValue(false, root.GetString(), null, null);
                }

                if (root.ValueKind != JsonValueKind.Object)
                {
                    return new SemanticValue(false, root.GetRawText(), null, null);
                }

                if (root.TryGetProperty("empty", out var empty) && empty.ValueKind == JsonValueKind.True)
                {
                    return new SemanticValue(true, null, null, null);
                }

                string? hero = null;
                int? star = null;
                IReadOnlyList<string>? items = null;

                if (root.TryGetProperty("hero", out var heroEl) && heroEl.ValueKind == JsonValueKind.String)
                {
                    hero = heroEl.GetString();
                }

                if (root.TryGetProperty("star", out var starEl)
                    && starEl.ValueKind == JsonValueKind.Number
                    && starEl.TryGetInt32(out var starValue))
                {
                    star = starValue;
                }

                if (root.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
                {
                    items = itemsEl.EnumerateArray()
                        .Where(static el => el.ValueKind == JsonValueKind.String)
                        .Select(static el => el.GetString()!)
                        .ToList();
                }

                return new SemanticValue(false, hero, star, items);
            }
        }
    }
}