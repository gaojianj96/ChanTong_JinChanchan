namespace ChanSight.Vision.Models;

public sealed class OcrTextResult
{
    public string RawText { get; }
    public string CleanedText { get; }
    public string? MatchedDictKey { get; }
    public float Confidence { get; }

    public OcrTextResult(string rawText, string cleanedText, string? matchedDictKey, float confidence)
    {
        RawText = rawText ?? throw new ArgumentNullException(nameof(rawText));
        CleanedText = cleanedText ?? throw new ArgumentNullException(nameof(cleanedText));
        if (confidence < 0 || confidence > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        MatchedDictKey = matchedDictKey;
        Confidence = confidence;
    }
}