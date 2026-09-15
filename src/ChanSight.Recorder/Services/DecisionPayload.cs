using System.Text.Json;

namespace ChanSight.Recorder.Services;

public sealed record DecisionPayload(string DecisionId, string Kind, string Verdict, string Reason, double Risk)
{
    public static DecisionPayload? TryParse(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!TryGetString(root, "decisionId", out var decisionId) || decisionId is null)
                return null;
            if (!TryGetString(root, "kind", out var kind) || kind is null)
                return null;
            if (!TryGetString(root, "verdict", out var verdict) || verdict is null)
                return null;
            if (!TryGetString(root, "reason", out var reason) || reason is null)
                return null;
            if (!TryGetNumber(root, "risk", out var risk) || risk < 0 || risk > 1)
                return null;

            return new DecisionPayload(decisionId, kind, verdict, reason, risk);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (FindProperty(element, propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetNumber(JsonElement element, string propertyName, out double value)
    {
        if (FindProperty(element, propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetDouble(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool FindProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}