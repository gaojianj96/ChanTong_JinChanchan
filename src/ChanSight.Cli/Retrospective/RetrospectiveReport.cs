namespace ChanSight.Cli.Retrospective;

public sealed record RetrospectiveFinding(string Claim, IReadOnlyList<long> EvidenceSeqs, double Confidence);

public sealed record RetrospectiveReport(
    string Summary,
    IReadOnlyList<RetrospectiveFinding> Findings,
    IReadOnlyList<string> NextActions,
    double Confidence);