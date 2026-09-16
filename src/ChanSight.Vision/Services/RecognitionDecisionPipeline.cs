using ChanSight.Core.Engine;
using ChanSight.Vision.Models;

namespace ChanSight.Vision.Services;

public sealed class RecognitionDecisionPipeline
{
    private readonly RecognitionToGameStateAdapter _adapter;
    private readonly GameStateManager _manager;
    private readonly DecisionPanelService _panel;

    public RecognitionDecisionPipeline(
        RecognitionToGameStateAdapter adapter,
        GameStateManager manager,
        DecisionPanelService panel)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _panel = panel ?? throw new ArgumentNullException(nameof(panel));
    }

    public DecisionPanelResult Process(RecognitionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _adapter.Apply(frame);
        var snapshot = _manager.Current;
        return _panel.EvaluateAlgorithm(snapshot);
    }

    public Task<DecisionPanelResult> ProcessWithAdvisorAsync(
        RecognitionFrame frame,
        AdvisorEvent e,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(e);

        _adapter.Apply(frame);
        var snapshot = _manager.Current;
        return _panel.EvaluateWithAdvisorAsync(snapshot, e, ct);
    }
}