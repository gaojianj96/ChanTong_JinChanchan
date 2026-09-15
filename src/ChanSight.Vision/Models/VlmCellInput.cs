namespace ChanSight.Vision.Models;

/// <summary>
/// A single cropped cell block to be classified by the VLM.
/// </summary>
public sealed record VlmCellInput(int CellIndex, byte[] ImageData, string Mime);