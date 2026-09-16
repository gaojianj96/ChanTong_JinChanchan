using ChanSight.Core.Engine;
using ChanSight.Vision.Models;

namespace ChanSight.Vision.Interfaces;

/// <summary>
/// Detects the current game phase from a recognition frame, either via a local
/// heuristic (deterministic, zero network) or an optional VLM enhancement.
/// </summary>
public interface IPhaseDetector
{
    GamePhase Detect(RecognitionFrame frame);

    Task<GamePhase> DetectAsync(RecognitionFrame frame, CancellationToken ct = default);
}