using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChanSight.Vision.Models;

/// <summary>
/// Source credibility tier for recognition metadata (T0 = most trusted, T3 = least).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourceTier>))]
public enum SourceTier
{
    T0,
    T1,
    T2,
    T3,
}

/// <summary>
/// Deterministic equipment atlas entry (icon id + count).
/// </summary>
public sealed record ItemStack(
    [property: JsonPropertyName("iconId")] string IconId,
    [property: JsonPropertyName("count")] int Count);

/// <summary>
/// A single unit cell (board or bench). Name is null when the slot is empty.
/// </summary>
public sealed record UnitCell(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("star")] int Star,
    [property: JsonPropertyName("items")] IReadOnlyList<ItemStack> Items,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("sourceTier")] SourceTier SourceTier);

/// <summary>
/// A single shop card slot. Semantic name + deterministic cost.
/// </summary>
public sealed record ShopCard(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("cost")] int Cost,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("sourceTier")] SourceTier SourceTier);

/// <summary>
/// A fully-labeled recognition frame. BoardCells fixed 28, BenchCells fixed 9, ShopCards fixed 5.
/// Deterministic fields (gold/level/stage/hp/exp/star/occupancy/items) come from local CV;
/// semantic fields (unit name, shop card name) come from VLM; audit metadata from source tier.
/// </summary>
public sealed record RecognitionFrame
{
    [JsonPropertyName("sourceTier")] public SourceTier SourceTier { get; init; }
    [JsonPropertyName("correctionFlag")] public bool CorrectionFlag { get; init; }
    [JsonPropertyName("timestamp")] public string Timestamp { get; init; } = string.Empty;
    [JsonPropertyName("confidence")] public double Confidence { get; init; }

    [JsonPropertyName("gold")] public int Gold { get; init; }
    [JsonPropertyName("level")] public int Level { get; init; }
    [JsonPropertyName("stage")] public string Stage { get; init; } = string.Empty;
    [JsonPropertyName("hp")] public int Hp { get; init; }
    [JsonPropertyName("exp")] public int Exp { get; init; }

    [JsonPropertyName("boardCells")] public IReadOnlyList<UnitCell> BoardCells { get; init; } = Array.Empty<UnitCell>();
    [JsonPropertyName("benchCells")] public IReadOnlyList<UnitCell> BenchCells { get; init; } = Array.Empty<UnitCell>();
    [JsonPropertyName("shopCards")] public IReadOnlyList<ShopCard> ShopCards { get; init; } = Array.Empty<ShopCard>();

    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter<SourceTier>() },
    };

    public static string Serialize(RecognitionFrame frame) =>
        JsonSerializer.Serialize(frame, SerializerOptions);

    public static RecognitionFrame Deserialize(string json) =>
        JsonSerializer.Deserialize<RecognitionFrame>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize RecognitionFrame.");
}