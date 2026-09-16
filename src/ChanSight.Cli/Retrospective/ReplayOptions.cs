namespace ChanSight.Cli.Retrospective;

public sealed record ReplayOptions(string ReplayPath, bool Export, string? ExportPath, bool Privacy);
