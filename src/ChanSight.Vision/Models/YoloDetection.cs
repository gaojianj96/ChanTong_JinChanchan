namespace ChanSight.Vision.Models;

using ChanSight.Vision.Interfaces;
using OpenCvSharp;

public sealed record YoloDetection(Rect Box, double Confidence, int ClassId) : IDetection;