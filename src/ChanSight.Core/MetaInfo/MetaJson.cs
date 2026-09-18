namespace ChanSight.Core.MetaInfo;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class MetaJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
