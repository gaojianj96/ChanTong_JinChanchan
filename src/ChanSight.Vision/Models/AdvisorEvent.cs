namespace ChanSight.Vision.Models;

/// <summary>
/// Key game nodes that trigger a global advice pass via a text LLM.
/// </summary>
public enum AdvisorEventType
{
    HexAugment,
    ThreeStar,
    PlayerEliminated,
    MajorTransition,
    ManualRequest
}

/// <summary>
/// Structured description of a key-node event. <see cref="Context"/> carries a
/// scene summary (free text or compact JSON) that is embedded in the prompt.
/// </summary>
public sealed record AdvisorEvent(AdvisorEventType Type, string Context);

/// <summary>
/// Text advice produced by the global advisor, with a rationale and a confidence
/// in the [0,1] range.
/// </summary>
public sealed record AdvisorSuggestion(string Suggestion, string Reason, double Confidence);