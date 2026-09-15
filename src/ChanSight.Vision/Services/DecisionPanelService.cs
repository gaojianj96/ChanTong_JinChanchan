using ChanSight.Core.Engine;
using ChanSight.Vision.Models;

namespace ChanSight.Vision.Services;

public sealed class DecisionPanelService
{
    private readonly ITacticalAdvisor _tacticalAdvisor;
    private readonly LLMAdvisor _llmAdvisor;

    public DecisionPanelService(ITacticalAdvisor tacticalAdvisor, LLMAdvisor llmAdvisor)
    {
        _tacticalAdvisor = tacticalAdvisor ?? throw new ArgumentNullException(nameof(tacticalAdvisor));
        _llmAdvisor = llmAdvisor ?? throw new ArgumentNullException(nameof(llmAdvisor));
    }

    public DecisionPanelResult EvaluateAlgorithm(GameStateSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var algorithmAdvice = _tacticalAdvisor.Evaluate(state);
        return new DecisionPanelResult(state, algorithmAdvice, AdvisorAdvice: null);
    }

    public async Task<DecisionPanelResult> EvaluateWithAdvisorAsync(
        GameStateSnapshot state,
        AdvisorEvent e,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(e);

        var algorithmAdvice = _tacticalAdvisor.Evaluate(state);
        var advisorAdvice = await _llmAdvisor.AdviseAsync(e, ct).ConfigureAwait(false);

        return new DecisionPanelResult(state, algorithmAdvice, advisorAdvice);
    }
}