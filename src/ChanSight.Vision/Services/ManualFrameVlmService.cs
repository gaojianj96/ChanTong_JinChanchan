using System.Text.Json;
using ChanSight.Core.Season;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

/// <summary>
/// Manually triggered whole-frame VLM recognition. Sends a single (aspect-ratio
/// preserved, downscaled) full screenshot directly to the VLM — no ROI crops —
/// then parses and validates a strict JSON contract into a
/// <see cref="RecognitionFrame"/>. Invalid fields are nulled out and recorded in
/// <see cref="RecognitionFrame.Issues"/> rather than crashing. The opponent
/// index is passed through verbatim (confirmed by the UI) rather than guessed;
/// the perspective is conveyed to the VLM via the prompt, not via extra crops.
/// </summary>
public sealed class ManualFrameVlmService
{
    private const int MaxLevenshteinDistance = 2;
    private const int BoardCellCount = 28;
    private const int BenchCellCount = 9;
    private const int MaxLongEdge = 1920;
    private const string CandidateSource = "vlm-recognition";

    private readonly IVlmClient _client;
    private readonly SeasonRuntime _runtime;
    private readonly ISeasonDictionaryWriter? _writer;
    private readonly RecognitionLogger? _logger;

    public ManualFrameVlmService(
        IVlmClient client,
        SeasonRuntime? runtime = null,
        ISeasonDictionaryWriter? writer = null,
        RecognitionLogger? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
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

        var prompt = BuildPrompt(isSelf);
        var images = BuildImages(fullFrame);

        var json = await _client.CompleteAsync(prompt, images, ct).ConfigureAwait(false);

        var frame = Parse(json, isSelf);
        _logger?.LogVlmRecognition(matchId ?? "unknown", source, frame, frame.Issues);
        return frame;
    }

    // A single full-frame image is sent: the VLM locates and reads every field
    // (HUD / board / bench / shop / opponents) itself from the one image.
    private IReadOnlyList<(string mime, byte[] data)> BuildImages(Mat fullFrame)
    {
        var resized = ResizeForVlm(fullFrame);
        var data = resized.Empty() ? Array.Empty<byte>() : resized.ImEncode(".jpg");
        return new List<(string mime, byte[] data)> { ("image/jpeg", data) };
    }

    private static Mat ResizeForVlm(Mat fullFrame)
    {
        if (fullFrame.Empty())
            return fullFrame;

        int maxEdge = Math.Max(fullFrame.Width, fullFrame.Height);
        if (maxEdge <= MaxLongEdge)
        {
            var unchanged = new Mat();
            fullFrame.CopyTo(unchanged);
            return unchanged;
        }

        double scale = (double)MaxLongEdge / maxEdge;
        int newWidth = Math.Max(1, (int)Math.Round(fullFrame.Width * scale));
        int newHeight = Math.Max(1, (int)Math.Round(fullFrame.Height * scale));

        var resized = new Mat();
        Cv2.Resize(fullFrame, resized, new Size(newWidth, newHeight), 0, 0, InterpolationFlags.Area);
        return resized;
    }

    private string BuildPrompt(bool isSelf)
    {
        var heroTable = string.Join("、", _runtime.Heroes);
        var itemTable = string.Join("、", _runtime.Items);
        var modeHint = GetModeHint(_runtime.Context.Mode);
        var perspectiveHint = isSelf
            ? "这是己方(本人)棋盘视角。"
            : "这是对手棋盘视角, 请识别画面中该名对手的全部信息(含右侧玩家列表中的对手序号与血量/经济)。";

        var prompt = $$"""
            下面是一张《金铲铲之战》的完整游戏截图(可能已适当缩放)。请你识别画面中的全部信息并作答。
            {{perspectiveHint}}

            画面包含:
            - 顶部 HUD 条(阶段/金币/等级/血量/经验)
            - 棋盘区域(28 格, 4 列 × 7 行, 含棋子星级与装备)
            - 备战席(9 格, 0-8 顺序)
            - 商店(底部 5 张卡, 若有)
            - 右侧玩家列表(对手序号/血量/经济)

            请从以下候选英雄全表中选取英雄名(空棋盘格填 null):
            {{heroTable}}

            装备只能从以下候选装备全表中选取:
            {{itemTable}}

            规则:
            1. 只输出一个 JSON 对象, 不要输出任何解释、前缀或 markdown 代码块。
            2. 空棋盘格 hero 填 null、star 填 0、items 填 []。
            3. 星级为 0-3; 装备从候选全表选。
            4. 无法确定的字段填 null 或 0, 并调低整体 confidence 标记低置信。
            5. 严格遵守以下 schema, 缺失字段用 null:
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
    public string BuildPromptForDisplay() => BuildPrompt(isSelf: true);

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