namespace ChanSight.Core.Data;

using System.Text.Json;

public sealed class CompKnowledgeBase
{
    private IReadOnlyList<CompTemplate> _templates = Array.Empty<CompTemplate>();

    public CompKnowledgeBase()
    {
    }

    public CompKnowledgeBase(string templatesDirectory)
    {
        Load(templatesDirectory);
    }

    public IReadOnlyList<CompTemplate> Templates => _templates;

    public void Load(string templatesDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatesDirectory);

        if (!Directory.Exists(templatesDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Comp template directory not found: {templatesDirectory}");
        }

        var files = Directory.GetFiles(templatesDirectory, "*.json", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
        {
            throw new InvalidOperationException(
                $"No .json comp template files found in: {templatesDirectory}");
        }

        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        var templates = new List<CompTemplate>(files.Length);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var template = LoadFile(file);
            ValidateTemplate(template, file, seenIds);
            templates.Add(template);
        }

        _templates = templates;
    }

    public IReadOnlyList<CompTemplate> QueryByUnits(IReadOnlyCollection<string> ownedUnits)
    {
        ArgumentNullException.ThrowIfNull(ownedUnits);

        if (ownedUnits.Count == 0)
        {
            return Array.Empty<CompTemplate>();
        }

        var owned = new HashSet<string>(ownedUnits, StringComparer.OrdinalIgnoreCase);

        var ranked = new List<(CompTemplate Template, int Hits)>(_templates.Count);

        foreach (var template in _templates)
        {
            var hits = CountHits(template.CoreUnits, owned) + CountHits(template.SecondaryUnits, owned);
            if (hits > 0)
            {
                ranked.Add((template, hits));
            }
        }

        return ranked
            .OrderByDescending(static x => x.Hits)
            .ThenBy(static x => x.Template.Id)
            .Select(static x => x.Template)
            .ToList();
    }

    private static CompTemplate LoadFile(string file)
    {
        string json;
        try
        {
            json = File.ReadAllText(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Failed to read comp template file: {file}", ex);
        }

        try
        {
            var template = JsonSerializer.Deserialize<CompTemplate>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return template ?? throw new InvalidOperationException(
                $"Comp template file is empty or null: {file}");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON in comp template file: {file}", ex);
        }
    }

    private static void ValidateTemplate(CompTemplate template, string file, ISet<string> seenIds)
    {
        if (string.IsNullOrWhiteSpace(template.Id))
        {
            throw new InvalidOperationException($"Comp template has empty id: {file}");
        }

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            throw new InvalidOperationException($"Comp template has empty name: {file}");
        }

        if (!seenIds.Add(template.Id))
        {
            throw new InvalidOperationException($"Duplicate comp template id '{template.Id}': {file}");
        }

        if (template.KeyTraits is null || template.KeyTraits.Count == 0 || template.KeyTraits.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"Comp template '{template.Id}' must have non-empty key traits: {file}");
        }

        if (template.CoreUnits is null || template.CoreUnits.Count == 0 || template.CoreUnits.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"Comp template '{template.Id}' must have non-empty core units: {file}");
        }

        if (template.SecondaryUnits is null || template.SecondaryUnits.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"Comp template '{template.Id}' has invalid secondary units: {file}");
        }

        if (string.IsNullOrWhiteSpace(template.Description))
        {
            throw new InvalidOperationException($"Comp template '{template.Id}' has empty description: {file}");
        }

        foreach (var trait in template.KeyTraits)
        {
            if (!KnownHeroes.Traits.Contains(trait))
            {
                throw new InvalidOperationException($"Comp template '{template.Id}' has unknown key trait '{trait}': {file}");
            }
        }

        foreach (var unit in template.CoreUnits.Concat(template.SecondaryUnits))
        {
            if (!KnownHeroes.Heroes.Contains(unit))
            {
                throw new InvalidOperationException($"Comp template '{template.Id}' has unknown hero '{unit}': {file}");
            }
        }
    }

    private static int CountHits(IReadOnlyList<string> units, HashSet<string> owned)
    {
        var hits = 0;
        for (var i = 0; i < units.Count; i++)
        {
            if (owned.Contains(units[i]))
            {
                hits++;
            }
        }

        return hits;
    }
}