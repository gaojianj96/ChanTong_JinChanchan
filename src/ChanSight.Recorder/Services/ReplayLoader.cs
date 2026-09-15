using System.Text.Json;

namespace ChanSight.Recorder.Services;

public sealed class ReplayLoader
{
    private readonly IFileSystem _fileSystem;

    public ReplayLoader()
        : this(new FileSystem())
    {
    }

    internal ReplayLoader(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public async Task<ReplayLoadResult> LoadAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ct.ThrowIfCancellationRequested();

        var content = await _fileSystem.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        if (content is null)
        {
            return new ReplayLoadResult
            {
                Issues = new[] { $"File not found: {filePath}" }
            };
        }

        var events = new List<MatchEvent>();
        var issues = new List<string>();

        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r').Trim();
            if (line.Length == 0)
            {
                continue;
            }

            ct.ThrowIfCancellationRequested();

            MatchEvent? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<MatchEvent>(line);
            }
            catch (JsonException ex)
            {
                issues.Add($"Line {i + 1} is not valid JSON: {ex.Message}");
                continue;
            }

            if (parsed is null)
            {
                issues.Add($"Line {i + 1} could not be deserialized into {nameof(MatchEvent)}.");
                continue;
            }

            events.Add(parsed);
        }

        foreach (var group in events.GroupBy(e => e.Seq).Where(g => g.Count() > 1))
        {
            issues.Add($"Duplicate Seq {group.Key}.");
        }

        events.Sort(static (a, b) => a.Seq.CompareTo(b.Seq));

        return new ReplayLoadResult
        {
            Events = events,
            Issues = issues
        };
    }

    public bool ValidateTimeline(IReadOnlyList<MatchEvent> events, out string issue)
    {
        ArgumentNullException.ThrowIfNull(events);
        issue = string.Empty;

        if (events.Count == 0)
        {
            return true;
        }

        if (events[0].Seq != 1)
        {
            issue = $"Timeline must start at Seq 1, found {events[0].Seq}.";
            return false;
        }

        for (var i = 1; i < events.Count; i++)
        {
            var previous = events[i - 1];
            var current = events[i];

            if (current.Seq <= previous.Seq)
            {
                issue = $"Seq out of order: Seq {current.Seq} follows Seq {previous.Seq}.";
                return false;
            }

            if (current.Seq != previous.Seq + 1)
            {
                issue = $"Seq gap detected: Seq {previous.Seq} followed by Seq {current.Seq}.";
                return false;
            }

            if (current.Timestamp < previous.Timestamp)
            {
                issue = $"Timestamp moved backwards: {current.Timestamp:O} follows {previous.Timestamp:O}.";
                return false;
            }
        }

        return true;
    }
}

public sealed record ReplayLoadResult
{
    public IReadOnlyList<MatchEvent> Events { get; init; } = Array.Empty<MatchEvent>();

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();
}