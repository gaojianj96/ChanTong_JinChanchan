using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChanSight.Core.Season;
using ChanSight.Recorder.Services;

namespace ChanSight.Cli.Retrospective;

/// <summary>
/// Loads one recorded match's event stream, runs the R2 algorithmic score and the
/// R3 LLM narrative report, then renders the retrospective panel to the console.
/// With the privacy switch enabled, <see cref="ReplayOptions.Privacy"/> causes the
/// game id and any known hero name to be replaced by <c>[redacted]</c> in both the
/// panel output and the optional JSON export.
/// </summary>
public sealed class ReplayCommand
{
    private const string Redacted = "[redacted]";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ReplayLoader _loader;
    private readonly RetrospectiveScorer _scorer;
    private readonly RetrospectiveReportGenerator _generator;
    private readonly SeasonRuntime _runtime;

    public ReplayCommand(
        ReplayLoader loader,
        RetrospectiveScorer scorer,
        RetrospectiveReportGenerator generator,
        SeasonRuntime? runtime = null)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _scorer = scorer ?? throw new ArgumentNullException(nameof(scorer));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _runtime = runtime ?? SeasonRuntime.CreateDefault();
    }

    public async Task RunAsync(ReplayOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var loadResult = await _loader.LoadAsync(options.ReplayPath, ct).ConfigureAwait(false);
        var events = loadResult.Events;
        var gameId = events.Count > 0 ? events[0].GameId : string.Empty;

        var score = _scorer.Score(events);
        var report = await _generator.GenerateAsync(events, ct).ConfigureAwait(false);

        var displayGameId = options.Privacy ? Redacted : gameId;
        var displayScore = options.Privacy ? RedactScore(score, gameId) : score;
        var displayReport = options.Privacy ? RedactReport(report, gameId) : report;

        PrintPanel(options.ReplayPath, displayGameId, displayScore, displayReport);

        if (options.Export)
        {
            var exportPath = options.ExportPath ?? DefaultExportPath(options.ReplayPath);
            var model = new ReplayExportModel(displayGameId, displayScore, displayReport);
            var json = JsonSerializer.Serialize(model, JsonOptions);
            await File.WriteAllTextAsync(exportPath, json, ct).ConfigureAwait(false);
        }
    }

    private static void PrintPanel(string replayPath, string gameId, RetrospectiveScore score, RetrospectiveReport report)
    {
        Console.WriteLine("== ChanSight replay ==");
        Console.WriteLine($"session: {replayPath}");
        Console.WriteLine($"game: {gameId}");
        Console.WriteLine();
        Console.WriteLine($"Summary: {report.Summary}");
        Console.WriteLine();
        Console.WriteLine("Findings:");
        foreach (var finding in report.Findings)
        {
            var seqs = string.Join(", ", finding.EvidenceSeqs.Select(s => s.ToString(CultureInfo.InvariantCulture)));
            Console.WriteLine($"  - [Seq {seqs}] ({Format(finding.Confidence)}) {finding.Claim}");
        }

        Console.WriteLine();
        Console.WriteLine("NextActions:");
        foreach (var action in report.NextActions)
        {
            Console.WriteLine($"  - {action}");
        }

        Console.WriteLine();
        Console.WriteLine("Score:");
        Console.WriteLine($"  Factual: stage={score.Factual.FinalStage} hp={score.Factual.FinalHp} outcome={score.Factual.Outcome}");
        Console.WriteLine($"  Expectation: decisions={score.Expectation.DecisionCount} avgRisk={Format(score.Expectation.AverageRisk)} confidence={Format(score.Expectation.ConfidenceScore)}");
        Console.WriteLine($"  Execution: decisionsWithAction={score.Execution.DecisionsWithAction}/{score.Execution.TotalDecisions} rate={Format(score.Execution.Rate)}");
        Console.WriteLine("  Luck:");
        foreach (var luck in score.Luck)
        {
            Console.WriteLine($"    - {luck.Label} (severity {Format(luck.Severity)}): {string.Join("; ", luck.Evidence)}");
        }

        if (score.Issues.Count > 0)
        {
            Console.WriteLine("  Issues:");
            foreach (var issue in score.Issues)
            {
                Console.WriteLine($"    - {issue}");
            }
        }
    }

    private RetrospectiveScore RedactScore(RetrospectiveScore score, string gameId)
    {
        return new RetrospectiveScore(
            score.Factual,
            score.Expectation,
            score.Execution,
            score.Luck.Select(l => new LuckAssessment(
                Redact(l.Label, gameId),
                l.Severity,
                l.Evidence.Select(e => Redact(e, gameId)).ToArray())).ToArray(),
            score.Issues.Select(i => Redact(i, gameId)).ToArray());
    }

    private RetrospectiveReport RedactReport(RetrospectiveReport report, string gameId)
    {
        return new RetrospectiveReport(
            Redact(report.Summary, gameId),
            report.Findings.Select(f => new RetrospectiveFinding(
                Redact(f.Claim, gameId),
                f.EvidenceSeqs,
                f.Confidence)).ToArray(),
            report.NextActions.Select(a => Redact(a, gameId)).ToArray(),
            report.Confidence);
    }

    private string Redact(string text, string gameId)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = text;
        if (!string.IsNullOrEmpty(gameId))
        {
            result = result.Replace(gameId, Redacted, StringComparison.Ordinal);
        }

        foreach (var hero in _runtime.Heroes)
        {
            result = result.Replace(hero, Redacted, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string DefaultExportPath(string replayPath)
    {
        var directory = Path.GetDirectoryName(replayPath);
        var fileName = Path.GetFileNameWithoutExtension(replayPath);
        var exportName = $"{fileName}.report.json";
        return string.IsNullOrEmpty(directory) ? exportName : Path.Combine(directory, exportName);
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
