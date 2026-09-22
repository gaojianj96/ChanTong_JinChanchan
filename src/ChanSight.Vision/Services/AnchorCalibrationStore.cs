using System.Text.Json;
using System.Text.Json.Serialization;
using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

/// <summary>
/// Persists a one-shot anchor calibration to <c>data/season/anchor-calibration.json</c>
/// and maps canonical board coordinates to current window coordinates. If the
/// current window size differs from the size at calibration time, the offset is
/// scaled by the (current / calibrated) ratio as an approximation (scale factors
/// themselves are assumed window-invariant).
/// </summary>
public sealed class AnchorCalibrationStore
{
    private const string FileName = "anchor-calibration.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly object _gate = new();
    private readonly string _rootDirectory;

    /// <summary>
    /// Creates the store over a root directory that must contain the calibration
    /// file at <c>{rootDirectory}/anchor-calibration.json</c> (callers typically
    /// pass the <c>data/season</c> folder).
    /// </summary>
    public AnchorCalibrationStore(string rootDirectory)
    {
        _rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
    }

    /// <summary>Creates the store over the default <c>data/season</c> base directory.</summary>
    public static AnchorCalibrationStore CreateDefault()
        => new(Path.Combine(AppContext.BaseDirectory, "data", "season"));

    public void Save(AnchorCalibrationResult result, double frameWidth, double frameHeight)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (frameWidth <= 0 || frameHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameWidth), "Frame dimensions must be positive.");
        }

        var dto = new CalibrationDto
        {
            IsValid = result.IsValid,
            Confidence = result.Confidence,
            ScaleX = result.ScaleX,
            ScaleY = result.ScaleY,
            OffsetX = result.OffsetX,
            OffsetY = result.OffsetY,
            MaxReprojectionErrorPx = result.MaxReprojectionErrorPx,
            FrameWidth = frameWidth,
            FrameHeight = frameHeight,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            ObservedAnchorPoints = result.ObservedAnchorPoints.Select(p => new double[] { p.X, p.Y }).ToList(),
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var path = GetFilePath();

        lock (_gate)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, json);
        }
    }

    public AnchorCalibrationResult? Load()
    {
        lock (_gate)
        {
            var dto = ReadDto();
            return dto is null ? null : ToResult(dto);
        }
    }

    public bool TryGet(out AnchorCalibrationResult? result)
    {
        result = Load();
        return result is not null;
    }

    /// <summary>
    /// Maps a canonical coordinate to the current window. If the current frame
    /// size equals the calibrated source size, scale/offset are applied
    /// directly; otherwise the offset is scaled by the (current / calibrated)
    /// ratio as an approximation. Returns null when no valid calibration exists.
    /// </summary>
    public Point2d? MapCanonical(Point2d canonical, int currentWidth, int currentHeight)
    {
        if (currentWidth <= 0 || currentHeight <= 0)
        {
            return null;
        }

        CalibrationDto? dto;
        lock (_gate)
        {
            dto = ReadDto();
        }

        if (dto is null || !dto.IsValid || dto.FrameWidth <= 0 || dto.FrameHeight <= 0)
        {
            return null;
        }

        double rx = currentWidth / dto.FrameWidth;
        double ry = currentHeight / dto.FrameHeight;

        return new Point2d(
            canonical.X * dto.ScaleX + dto.OffsetX * rx,
            canonical.Y * dto.ScaleY + dto.OffsetY * ry);
    }

    private string GetFilePath() => Path.Combine(_rootDirectory, FileName);

    private CalibrationDto? ReadDto()
    {
        var path = GetFilePath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CalibrationDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static AnchorCalibrationResult ToResult(CalibrationDto dto) => new()
    {
        IsValid = dto.IsValid,
        Confidence = dto.Confidence,
        ScaleX = dto.ScaleX,
        ScaleY = dto.ScaleY,
        OffsetX = dto.OffsetX,
        OffsetY = dto.OffsetY,
        MaxReprojectionErrorPx = dto.MaxReprojectionErrorPx,
        ObservedAnchorPoints = dto.ObservedAnchorPoints?
            .Where(p => p is { Length: >= 2 })
            .Select(p => new Point2d(p[0], p[1]))
            .ToArray() ?? Array.Empty<Point2d>(),
    };

    private sealed class CalibrationDto
    {
        [JsonPropertyName("isValid")] public bool IsValid { get; set; }
        [JsonPropertyName("confidence")] public double Confidence { get; set; }
        [JsonPropertyName("scaleX")] public double ScaleX { get; set; }
        [JsonPropertyName("scaleY")] public double ScaleY { get; set; }
        [JsonPropertyName("offsetX")] public double OffsetX { get; set; }
        [JsonPropertyName("offsetY")] public double OffsetY { get; set; }
        [JsonPropertyName("maxReprojectionErrorPx")] public double MaxReprojectionErrorPx { get; set; }
        [JsonPropertyName("frameWidth")] public double FrameWidth { get; set; }
        [JsonPropertyName("frameHeight")] public double FrameHeight { get; set; }
        [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = string.Empty;
        [JsonPropertyName("observedAnchorPoints")] public List<double[]>? ObservedAnchorPoints { get; set; }
    }
}