using System.Text.Json;
using ChanSight.Core.Season;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

/// <summary>
/// Manually triggered whole-frame VLM recognition. Crops a full screenshot into
/// region images (HUD / board / bench / opponents), sends them as a multi-image
/// prompt, then parses and validates a strict JSON contract into a
/// <see cref="RecognitionFrame"/>. Invalid fields are nulled out and recorded in
/// <see cref="RecognitionFrame.Issues"/> rather than crashing. The opponent
/// index is passed through verbatim (confirmed by the UI) rather than guessed.
/// </summary>
public sealed class ManualFrameVlmService
{
    private const int MaxLevenshteinDistance = 2;
    private const int BoardCellCount = 28;
    private const int BenchCellCount = 9;
    private const string CandidateSource = "vlm-recognition";

    private readonly IVlmClient _client;
    private readonly IRoiMapperService _roiMapper;
    private readonly SeasonRuntime _runtime;
    private readonly ISeasonDictionaryWriter? _writer;
    private readonly RecognitionLogger? _logger;

    public ManualFrameVlmService(
        IVlmClient client,
        IRoiMapperService roiMapper,
        SeasonRuntime? runtime = null,
        ISeasonDictionaryWriter? writer = null,
        RecognitionLogger? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _roiMapper = roiMapper ?? throw new ArgumentNullException(nameof(roiMapper));
        _runtime = runtime ?? SeasonRuntime.CreateDefault();
        _writer = writer;
        _logger = logger;
    }

    public async Task<RecognitionFrame> RecognizeAsync(
        Mat fullFrame,
        bool isSelf,
        CancellationToken ct = default,
        string? matchId = null,
        string source = "manual-vlm")
    {
        ArgumentNullException.ThrowIfNull(fullFrame);

        var prompt = BuildPrompt();
        var images = BuildImages(fullFrame, isSelf);

        var json = await _client.CompleteAsync(prompt, images, ct).ConfigureAwait(false);

        var frame = Parse(json, isSelf);
        _logger?.LogVlmRecognition(matchId ?? "unknown", source, frame, frame.Issues);
        return frame;
    }

    // Image order must match the numbered description in BuildPrompt: the VLM
    // relies on the positional ordering to know which crop is which.
    private IReadOnlyList<(string mime, byte[] data)> BuildImages(Mat fullFrame, bool isSelf)
    {
        var images = new List<(string mime, byte[] data)>
        {
            ("image/png", Encode(_roiMapper.CropRoi(fullFrame, RoiRegionType.StageRound))),
            ("image/png", Encode(_roiMapper.CropRoi(fullFrame, RoiRegionType.Hp))),
            ("image/png", Encode(_roiMapper.CropRoi(fullFrame, RoiRegionType.Level))),
            ("image/png", Encode(_roiMapper.CropRoi(fullFrame, RoiRegionType.Gold))),
            ("image/png", Encode(_roiMapper.CropRoi(fullFrame, RoiRegionType.BoardArea))),
            ("image/png", Encode(_roiMapper.CropRoi(fullFrame, RoiRegionType.PlayerBench))),
        };

        if (!isSelf)
        {
            images.Add(("image/png", Encode(_roiMapper.CropRoi(fullFrame, RoiRegionType.OpponentsSidebar))));
        }

        return images;
    }

    private static byte[] Encode(Mat crop)
    {
        if (crop.Empty())
            return Array.Empty<byte>();

        return crop.ImEncode(".png");
    }

    private string BuildPrompt()
    {
        var heroTable = string.Join("、", _runtime.Heroes);
        var itemTable = string.Join("、", _runtime.Items);
        var modeHint = GetModeHint(_runtime.Context.Mode);

        var prompt = $$"""
            你是《金铲铲之战》整帧识别助手。下面按顺序给出多张裁剪区域截图:
            1. 顶部 HUD 条(阶段/回合/血量/等级/金币)
            2. 棋盘区域(28 格, 按 0-27 行列顺序)
            3. 备战席(9 格, 0-8 顺序)
            4. (对手视角时)对手信息条(玩家名/序号/经济/等级)

            请从以下候选英雄全表中选取英雄名(空格子填 null):
            {{heroTable}}

            装备只能从以下候选装备全表中选取:
            {{itemTable}}

            输出要求:
            1. 只输出一个 JSON 对象, 不要输出任何解释、前缀或 markdown 代码块。
            2. 严格遵守以下 schema, 缺失字段用 null:
            {
              "perspective": "self" 或 "opponent",
              "opponentIndex": 0-6 的整数或 null (对手视角时的对手序号, 无法确定填 null),
              "playerName": "玩家名" 或 null,
              "stage": "如 3-2",
              "hp": 整数, "level": 整数, "exp": 整数, "gold": 整数,
              "goldEstimate": 0/10/20/30/40/50 的下限档位或 null (对手经济; 己方为 null),
              "streak": 整数,
              "board": [ {"slot":0, "hero":"盖伦", "star":2, "items":["羊刀"]}, ... 共 28 项 ],
              "bench": [ {"slot":0, "hero":null, "star":0, "items":[]}, ... 共 9 项 ],
              "confidence": 0 到 1 之间的浮点数
            }
            3. 空格子 hero 填 null、star 填 0、items 填 []。无法确定 entire 字段时用 null。
            """;

        if (!string.IsNullOrEmpty(modeHint))
        {
            prompt += $"""

                模式说明:
                {modeHint}
                """;
        }

        return prompt;
    }

    /// <summary>
    /// 供字典查看界面只读展示当前 season + mode 的完整 prompt 文本。
    /// 仅暴露读取入口, 不改动任何识别/解析逻辑。
    /// </summary>
    public string BuildPromptForDisplay() => BuildPrompt();

    /// <summary>
    /// 按 mode 返回追加到 prompt 的模式说明文本(内置默认字典, 可后续外部配置)。
    /// 未知 mode 返回空字符串(不追加); 只影响 prompt 文本, 不改 JSON schema/字典候选。
    /// </summary>
    public static string GetModeHint(string? mode) => (mode ?? string.Empty) switch
    {
        "恭喜发财" => "该模式为恭喜发财, 注意可能出现的特殊机制/高费阵容/专属强化, 高经济节奏下可更激进地升人口与搜牌。",
        "匹配" => "该模式为标准匹配, 按常规经济节奏均衡运营升人口与搜牌。",
        "狂暴" => "该模式为狂暴, 节奏更快、经济更紧, 升人口与搜牌需更果断, 注意早期定阵。",
        _ => string.Empty,
    };

    private RecognitionFrame Parse(string json, bool isSelf)
    {
        var issues = new List<string>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return EmptyFrame(isSelf, new[] { "VLM 输出不是 JSON 对象。" });
        }

        var perspective = StringProp(root, "perspective");
        var opponentIndex = IntProp(root, "opponentIndex");
        var playerName = StringProp(root, "playerName");

        var stage = StringProp(root, "stage") ?? string.Empty;
        var hp = IntProp(root, "hp") ?? 0;
        var level = IntProp(root, "level") ?? 0;
        var exp = IntProp(root, "exp") ?? 0;
        var gold = IntProp(root, "gold") ?? 0;

        int? goldEstimate = IntProp(root, "goldEstimate");
        if (goldEstimate.HasValue && !IsValidGoldEstimate(goldEstimate.Value))
        {
            issues.Add($"goldEstimate 非法档位 {goldEstimate.Value}, 已置 null。");
            goldEstimate = null;
        }

        var confidence = DoubleProp(root, "confidence") ?? 0.0;

        var board = ArrayProp(root, "board", issues, isSelf);
        var bench = ArrayProp(root, "bench", issues, isSelf);

        if (board.Count != BoardCellCount)
            issues.Add($"board 应为 {BoardCellCount} 项, 实际 {board.Count}。");
        if (bench.Count != BenchCellCount)
            issues.Add($"bench 应为 {BenchCellCount} 项, 实际 {bench.Count}。");

        return new RecognitionFrame
        {
            SourceTier = SourceTier.T2,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            Confidence = confidence,
            Gold = gold,
            Level = level,
            Stage = stage,
            Hp = hp,
            Exp = exp,
            PlayerName = playerName,
            GoldEstimate = goldEstimate,
            OpponentIndex = opponentIndex,
            Perspective = perspective,
            BoardCells = board,
            BenchCells = bench,
            Issues = issues,
        };
    }

    private IReadOnlyList<UnitCell> ArrayProp(JsonElement root, string name, List<string> issues, bool isSelf)
    {
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Array)
        {
            issues.Add($"{name} 缺失或非数组。");
            return Array.Empty<UnitCell>();
        }

        var cells = new List<UnitCell>();
        foreach (var element in prop.EnumerateArray())
        {
            cells.Add(ParseCell(element, name, issues));
        }

        return cells;
    }

    private UnitCell ParseCell(JsonElement element, string arrayName, List<string> issues)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            issues.Add($"{arrayName} 中存在非对象项, 已置空。");
            return EmptyCell();
        }

        double confidence = 0.0;
        if (element.TryGetProperty("confidence", out var confProp) && confProp.TryGetDouble(out var c))
            confidence = c;

        string? name = null;
        if (element.TryGetProperty("hero", out var heroProp) && heroProp.ValueKind == JsonValueKind.String)
        {
            var raw = heroProp.GetString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                name = GameSeasonDictionary.TryFuzzyMatch(
                    raw!,
                    _runtime.Heroes,
                    MaxLevenshteinDistance);
                if (name is null)
                {
                    issues.Add($"{arrayName} 英雄名 \"{raw}\" 不在字典内, 已置 null。");
                    AddCandidate(raw!, confidence);
                }
            }
        }
        else if (element.TryGetProperty("name", out var nameProp) &&
                 nameProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(nameProp.GetString()))
        {
            name = nameProp.GetString();
        }

        var star = element.TryGetProperty("star", out var starProp) && starProp.TryGetInt32(out var s) ? s : 0;
        if (star is < 0 or > 3)
        {
            issues.Add($"{arrayName} 星级 {star} 越界, 已归零。");
            star = 0;
        }

        var items = ParseItems(element, arrayName, issues, confidence);

        if (string.IsNullOrWhiteSpace(name) && star == 0 && items.Count == 0)
            return EmptyCell();

        return new UnitCell(name, star, items, confidence, SourceTier.T2);
    }

    private IReadOnlyList<ItemStack> ParseItems(JsonElement element, string arrayName, List<string> issues, double confidence)
    {
        if (!element.TryGetProperty("items", out var itemsProp) || itemsProp.ValueKind != JsonValueKind.Array)
            return Array.Empty<ItemStack>();

        var result = new List<ItemStack>();
        foreach (var item in itemsProp.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                continue;

            var raw = item.GetString();
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var matched = GameSeasonDictionary.TryFuzzyMatch(raw!, _runtime.Items, MaxLevenshteinDistance);
            if (matched is null)
            {
                issues.Add($"{arrayName} 装备名 \"{raw}\" 不在字典内。");
                AddCandidate(raw!, confidence);
                matched = raw;
            }

            result.Add(new ItemStack(matched, 1));
        }

        return result;
    }

    private void AddCandidate(string entity, double confidence)
    {
        _writer?.AddCandidate(
            _runtime.Context.SeasonId,
            new CandidateMeta(entity, CandidateSource, null, confidence, DateTimeOffset.UtcNow));
    }

    private static UnitCell EmptyCell() => new(null, 0, Array.Empty<ItemStack>(), 0.0, SourceTier.T3);

    private static RecognitionFrame EmptyFrame(bool isSelf, IReadOnlyList<string> issues) => new()
    {
        SourceTier = SourceTier.T2,
        Perspective = isSelf ? "self" : "opponent",
        Issues = issues,
    };

    private static bool IsValidGoldEstimate(int value) =>
        value is 0 or 10 or 20 or 30 or 40 or 50;

    private static string? StringProp(JsonElement root, string name) =>
        root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? (string.IsNullOrWhiteSpace(prop.GetString()) ? null : prop.GetString())
            : null;

    private static int? IntProp(JsonElement root, string name) =>
        root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var v)
            ? v
            : null;

    private static double? DoubleProp(JsonElement root, string name) =>
        root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var v)
            ? v
            : null;
}