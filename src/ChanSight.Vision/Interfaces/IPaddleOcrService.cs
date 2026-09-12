using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Interfaces;

public interface IPaddleOcrService
{
    IReadOnlyList<DetectedShopCard> RecognizeShopCards(Mat frame);

    OcrTextResult RecognizeGold(Mat frame);

    OcrTextResult RecognizeStage(Mat frame);
}