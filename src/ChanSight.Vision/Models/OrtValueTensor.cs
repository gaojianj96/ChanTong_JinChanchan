namespace ChanSight.Vision.Models;

public sealed record OrtValueTensor
{
    public float[] Data { get; }
    public long[] Shape { get; }
    public string Name { get; }

    public OrtValueTensor(string name, float[] data, long[] shape)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Shape = shape ?? throw new ArgumentNullException(nameof(shape));

        long expectedLength = 1;
        foreach (var dim in shape)
            expectedLength *= dim;

        if (data.Length != expectedLength)
            throw new ArgumentException(
                $"Data length {data.Length} does not match shape product {expectedLength}");
    }

    public int ElementCount => Data.Length;
}