namespace ChanSight.Vision.Services;

using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using OpenCvSharp;

public sealed class ClassicPostProcessor<TDetection> : IPostProcessor<TDetection> where TDetection : IDetection
{
    public IReadOnlyList<TDetection> Process(IReadOnlyList<TDetection> candidates, PostProcessOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(options);
        if (options.ConfidenceThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(options.ConfidenceThreshold), "Must be within [0,1].");
        if (options.NmsThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(options.NmsThreshold), "Must be within [0,1].");
        if (options.MaxDetections < 0)
            throw new ArgumentOutOfRangeException(nameof(options.MaxDetections), "Must be non-negative.");
        cancellationToken.ThrowIfCancellationRequested();

        if (candidates.Count == 0)
            return Array.Empty<TDetection>();

        var filtered = new List<TDetection>(candidates.Count);
        foreach (var d in candidates)
        {
            if (d.Confidence < options.ConfidenceThreshold)
                continue;

            if (options.RoiBounds is Rect roi)
            {
                var cx = d.Box.X + d.Box.Width / 2;
                var cy = d.Box.Y + d.Box.Height / 2;
                if (cx < roi.X || cx >= roi.X + roi.Width || cy < roi.Y || cy >= roi.Y + roi.Height)
                    continue;
            }

            filtered.Add(d);
        }

        if (filtered.Count == 0)
            return Array.Empty<TDetection>();

        filtered.Sort((a, b) =>
        {
            var conf = b.Confidence.CompareTo(a.Confidence);
            if (conf != 0)
                return conf;

            var cls = a.ClassId.CompareTo(b.ClassId);
            if (cls != 0)
                return cls;

            var x = a.Box.X.CompareTo(b.Box.X);
            return x != 0 ? x : a.Box.Y.CompareTo(b.Box.Y);
        });

        var kept = new List<TDetection>(filtered.Count);
        var suppressed = new bool[filtered.Count];

        for (var i = 0; i < filtered.Count; i++)
        {
            if (suppressed[i])
                continue;

            kept.Add(filtered[i]);

            for (var j = i + 1; j < filtered.Count; j++)
            {
                if (suppressed[j] || filtered[i].ClassId != filtered[j].ClassId)
                    continue;

                var iou = ComputeIoU(filtered[i].Box, filtered[j].Box);
                if (iou > options.NmsThreshold)
                    suppressed[j] = true;
            }
        }

        if (kept.Count > options.MaxDetections)
            return kept.GetRange(0, options.MaxDetections);

        return kept;
    }

    private static double ComputeIoU(in Rect a, in Rect b)
    {
        var ax1 = (double)a.X;
        var ay1 = (double)a.Y;
        var ax2 = (double)a.X + a.Width;
        var ay2 = (double)a.Y + a.Height;
        var bx1 = (double)b.X;
        var by1 = (double)b.Y;
        var bx2 = (double)b.X + b.Width;
        var by2 = (double)b.Y + b.Height;

        var x1 = Math.Max(ax1, bx1);
        var y1 = Math.Max(ay1, by1);
        var x2 = Math.Min(ax2, bx2);
        var y2 = Math.Min(ay2, by2);

        var interW = x2 - x1;
        var interH = y2 - y1;

        if (interW <= 0 || interH <= 0)
            return 0.0;

        var interArea = interW * interH;
        var unionArea = (ax2 - ax1) * (ay2 - ay1) + (bx2 - bx1) * (by2 - by1) - interArea;
        return interArea / unionArea;
    }
}