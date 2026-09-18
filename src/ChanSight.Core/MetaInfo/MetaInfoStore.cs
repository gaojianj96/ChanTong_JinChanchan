namespace ChanSight.Core.MetaInfo;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class MetaInfoStore
{
    private IReadOnlyList<CompMeta> _comps = Array.Empty<CompMeta>();
    private IReadOnlyDictionary<string, CompMeta> _byId = new Dictionary<string, CompMeta>(StringComparer.Ordinal);
    private VersionMeta? _version;
    private CompRelation[] _relations = Array.Empty<CompRelation>();
    private Tip[] _tips = Array.Empty<Tip>();
    private IReadOnlyList<string> _issues = Array.Empty<string>();

    public MetaInfoStore()
    {
    }

    public MetaInfoStore(string patchDirectory)
    {
        Load(patchDirectory);
    }

    public IReadOnlyList<CompMeta> Comps => _comps;

    public VersionMeta? Version => _version;

    public CompRelation[] Relations => _relations;

    public Tip[] Tips => _tips;

    public IReadOnlyList<string> Issues => _issues;

    public void Load(string patchDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patchDirectory);

        var issues = new List<string>();

        _version = ReadObject<VersionMeta>(Path.Combine(patchDirectory, "version.json"), issues);
        var comps = ReadList<CompMeta>(Path.Combine(patchDirectory, "comps.json"), issues, CreateBoardOptions(issues.Add));
        _relations = ReadList<CompRelation>(Path.Combine(patchDirectory, "relations.json"), issues).ToArray();
        _tips = ReadList<Tip>(Path.Combine(patchDirectory, "tips.json"), issues).ToArray();

        var validComps = ValidateComps(comps, issues);
        _comps = validComps;
        _byId = validComps.ToDictionary(static c => c.Id, StringComparer.Ordinal);

        ValidateTiers(_version, issues);
        ValidateRelations(_relations, issues);
        ValidateConfidence(issues);

        _issues = issues;
    }

    public void LoadAll(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        if (!Directory.Exists(rootDirectory))
        {
            Reset();
            return;
        }

        var bestDirectory = (string?)null;
        var bestKey = (string?)null;

        foreach (var directory in Directory.GetDirectories(rootDirectory))
        {
            var patch = TryReadPatch(Path.Combine(directory, "version.json"));
            var key = patch ?? Path.GetFileName(directory) ?? directory;
            if (bestDirectory is null || string.CompareOrdinal(key, bestKey) > 0)
            {
                bestDirectory = directory;
                bestKey = key;
            }
        }

        if (bestDirectory is null)
        {
            Reset();
            return;
        }

        Load(bestDirectory);
    }

    public CompMeta? GetById(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _byId.TryGetValue(id, out var comp) ? comp : null;
    }

    public IReadOnlyList<CompMeta> QueryByUnits(IReadOnlyCollection<string> ownedHeroNames)
    {
        ArgumentNullException.ThrowIfNull(ownedHeroNames);

        if (ownedHeroNames.Count == 0)
        {
            return Array.Empty<CompMeta>();
        }

        var owned = new HashSet<string>(ownedHeroNames, StringComparer.OrdinalIgnoreCase);
        var ranked = new List<(CompMeta Comp, int Hits)>(_comps.Count);

        foreach (var comp in _comps)
        {
            var hits = CountHits(comp.Units, owned);
            if (hits > 0)
            {
                ranked.Add((comp, hits));
            }
        }

        return ranked
            .OrderByDescending(static x => x.Hits)
            .ThenBy(static x => x.Comp.Id, StringComparer.Ordinal)
            .Select(static x => x.Comp)
            .ToList();
    }

    public IReadOnlyList<TierEntry> QueryTiers()
    {
        return _version?.Tiers ?? Array.Empty<TierEntry>();
    }

    public IReadOnlyList<CompRelation> RelationsOf(string compId)
    {
        ArgumentNullException.ThrowIfNull(compId);

        return _relations
            .Where(r => string.Equals(r.FromCompId, compId, StringComparison.Ordinal)
                     || string.Equals(r.ToCompId, compId, StringComparison.Ordinal))
            .ToArray();
    }

    public string? CompCodeOf(string compId)
    {
        return GetById(compId)?.CompCode;
    }

    private void Reset()
    {
        _comps = Array.Empty<CompMeta>();
        _byId = new Dictionary<string, CompMeta>(StringComparer.Ordinal);
        _version = null;
        _relations = Array.Empty<CompRelation>();
        _tips = Array.Empty<Tip>();
        _issues = Array.Empty<string>();
    }

    private static T? ReadObject<T>(string path, List<string> issues, JsonSerializerOptions? options = null)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, options ?? MetaJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            issues.Add($"Failed to parse {Path.GetFileName(path)}: {ex.Message}");
            return default;
        }
    }

    private static List<T> ReadList<T>(string path, List<string> issues, JsonSerializerOptions? options = null)
    {
        return ReadObject<List<T>>(path, issues, options) ?? [];
    }

    private static JsonSerializerOptions CreateBoardOptions(Action<string> onIssue)
    {
        var options = new JsonSerializerOptions(MetaJson.Options);
        options.Converters.Add(new BoardLayoutConverter(onIssue));
        return options;
    }

    private static IReadOnlyList<CompMeta> ValidateComps(List<CompMeta> comps, List<string> issues)
    {
        var valid = new List<CompMeta>(comps.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var comp in comps)
        {
            if (string.IsNullOrWhiteSpace(comp.Id))
            {
                issues.Add("CompMeta with empty Id was skipped");
                continue;
            }

            if (!seen.Add(comp.Id))
            {
                issues.Add($"Duplicate CompMeta.Id '{comp.Id}' ignored (keeping first occurrence)");
                continue;
            }

            if (string.IsNullOrWhiteSpace(comp.Name))
            {
                issues.Add($"Comp '{comp.Id}' has an empty Name");
            }

            if (comp.IntrinsicRequirements is null)
            {
                issues.Add($"Comp '{comp.Id}' has null IntrinsicRequirements");
            }

            valid.Add(comp);
        }

        return valid;
    }

    private void ValidateTiers(VersionMeta? version, List<string> issues)
    {
        if (version?.Tiers is null)
        {
            return;
        }

        foreach (var tier in version.Tiers)
        {
            if (!_byId.ContainsKey(tier.CompId))
            {
                issues.Add($"TierEntry references missing CompId '{tier.CompId}'");
            }
        }
    }

    private void ValidateRelations(IReadOnlyList<CompRelation> relations, List<string> issues)
    {
        foreach (var relation in relations)
        {
            if (!_byId.ContainsKey(relation.FromCompId))
            {
                issues.Add($"CompRelation FromCompId '{relation.FromCompId}' references a missing comp");
            }

            if (!_byId.ContainsKey(relation.ToCompId))
            {
                issues.Add($"CompRelation ToCompId '{relation.ToCompId}' references a missing comp");
            }
        }
    }

    private void ValidateConfidence(List<string> issues)
    {
        foreach (var comp in _comps)
        {
            CheckConfidence(comp.Source, $"comp '{comp.Id}'", issues);
        }

        if (_version?.Source is not null)
        {
            CheckConfidence(_version.Source, "version", issues);
        }

        if (_version?.Tiers is not null)
        {
            foreach (var tier in _version.Tiers)
            {
                CheckConfidence(tier.Source, $"tier '{tier.CompId}'", issues);
            }
        }

        foreach (var relation in _relations)
        {
            CheckConfidence(relation.Source, $"relation '{relation.FromCompId}' -> '{relation.ToCompId}'", issues);
        }

        foreach (var tip in _tips)
        {
            CheckConfidence(tip.Source, $"tip '{tip.Id}'", issues);
        }
    }

    private static void CheckConfidence(MetaSource source, string context, List<string> issues)
    {
        if (source.Confidence < 0d || source.Confidence > 1d)
        {
            issues.Add($"Confidence out of range [0,1] on {context}: {source.Confidence}");
        }
    }

    private static int CountHits(IReadOnlyList<UnitBuild> units, HashSet<string> owned)
    {
        var hits = 0;
        for (var i = 0; i < units.Count; i++)
        {
            var hero = units[i]?.HeroName;
            if (!string.IsNullOrWhiteSpace(hero) && owned.Contains(hero))
            {
                hits++;
            }
        }

        return hits;
    }

    private static string? TryReadPatch(string versionPath)
    {
        if (!File.Exists(versionPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(versionPath);
            return JsonSerializer.Deserialize<VersionMeta>(json, MetaJson.Options)?.Patch;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private sealed class BoardLayoutConverter : JsonConverter<BoardLayout>
    {
        private readonly Action<string> _onIssue;

        public BoardLayoutConverter(Action<string> onIssue)
        {
            _onIssue = onIssue;
        }

        public override BoardLayout Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                _onIssue("BoardLayout is not a JSON object");
                return new BoardLayout();
            }

            var placements = new List<BoardPlacement>();
            string? positioningNote = null;

            foreach (var property in root.EnumerateObject())
            {
                if (NameIs(property, "positioningNote"))
                {
                    positioningNote = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : property.Value.GetString();
                }
                else if (NameIs(property, "placements") && property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in property.Value.EnumerateArray())
                    {
                        var placement = TryReadPlacement(element);
                        if (placement is not null)
                        {
                            placements.Add(placement);
                        }
                    }
                }
            }

            return new BoardLayout { Placements = placements, PositioningNote = positioningNote };
        }

        public override void Write(Utf8JsonWriter writer, BoardLayout value, JsonSerializerOptions options)
        {
            throw new NotSupportedException("MetaInfoStore does not serialize BoardLayout.");
        }

        private BoardPlacement? TryReadPlacement(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                _onIssue("BoardPlacement is not a JSON object");
                return null;
            }

            string? heroName = null;
            var row = -1;
            var col = -1;

            foreach (var property in element.EnumerateObject())
            {
                if (NameIs(property, "heroName") && property.Value.ValueKind == JsonValueKind.String)
                {
                    heroName = property.Value.GetString();
                }
                else if (NameIs(property, "row") && property.Value.ValueKind == JsonValueKind.Number)
                {
                    row = property.Value.GetInt32();
                }
                else if (NameIs(property, "col") && property.Value.ValueKind == JsonValueKind.Number)
                {
                    col = property.Value.GetInt32();
                }
            }

            if (string.IsNullOrWhiteSpace(heroName))
            {
                _onIssue("BoardPlacement has an empty HeroName");
                return null;
            }

            if (row < 0 || row > BoardPlacement.MaxRow)
            {
                _onIssue($"BoardPlacement HeroName '{heroName}' has Row out of range [0..{BoardPlacement.MaxRow}]: {row}");
                return null;
            }

            if (col < 0 || col > BoardPlacement.MaxCol)
            {
                _onIssue($"BoardPlacement HeroName '{heroName}' has Col out of range [0..{BoardPlacement.MaxCol}]: {col}");
                return null;
            }

            return new BoardPlacement(heroName, row, col);
        }

        private static bool NameIs(JsonProperty property, string name)
        {
            return string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase);
        }
    }
}