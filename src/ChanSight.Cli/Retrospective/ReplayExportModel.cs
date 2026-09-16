using ChanSight.Recorder.Services;

namespace ChanSight.Cli.Retrospective;

/// <summary>
/// Serializable shape of the exported replay report: the algorithmic score plus
/// the LLM-generated narrative. With the privacy switch enabled, <see cref="GameId"/>
/// and any known hero names are replaced by <c>[redacted]</c> before serialization.
/// </summary>
public sealed record ReplayExportModel(
    string GameId,
    RetrospectiveScore Score,
    RetrospectiveReport Report);
