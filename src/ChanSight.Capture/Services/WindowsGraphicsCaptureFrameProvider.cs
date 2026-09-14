using System.Runtime.InteropServices;
using ChanSight.Core.Models;
using OpenCvSharp;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using static Vortice.Direct3D11.D3D11;

namespace ChanSight.Capture.Services;

internal sealed class WindowsGraphicsCaptureFrameProvider : IWgcFrameProvider
{
    private readonly object syncRoot = new();
    private ID3D11Device? device;
    private ID3D11DeviceContext? context;
    private IDirect3DDevice? winRtDevice;
    private Direct3D11CaptureFramePool? framePool;
    private GraphicsCaptureSession? session;
    private GraphicsCaptureItem? item;
    private long sequenceNumber;
    private bool running;

    public event EventHandler<CapturedFrame>? FrameReady;

    public event EventHandler? CaptureEnded;

    public ValueTask StartAsync(WindowTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        if (!GraphicsCaptureSession.IsSupported())
        {
            throw new PlatformNotSupportedException("Windows.Graphics.Capture is not supported on this system.");
        }

        lock (syncRoot)
        {
            if (running)
            {
                throw new InvalidOperationException("WGC frame provider is already running.");
            }

            CreateDevice();
            item = GraphicsCaptureItemInterop.CreateForWindow(target.Hwnd);
            item.Closed += OnItemClosed;
            framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                winRtDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                item.Size);
            session = framePool.CreateCaptureSession(item);
            framePool.FrameArrived += OnFrameArrived;
            session.StartCapture();
            running = true;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            StopAndRelease();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        lock (syncRoot)
        {
            StopAndRelease();
            context?.Dispose();
            context = null;
            device?.Dispose();
            device = null;
            winRtDevice?.Dispose();
            winRtDevice = null;
        }

        return ValueTask.CompletedTask;
    }

    private void CreateDevice()
    {
        var featureLevels = new[]
        {
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_1,
            FeatureLevel.Level_10_0
        };

        D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            featureLevels,
            out device,
            out _,
            out context).CheckError();

        using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
        winRtDevice = Direct3D11Helper.CreateDirect3DDevice(dxgiDevice.NativePointer);
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        CapturedFrame? capturedFrame = null;

        try
        {
            using var frame = sender.TryGetNextFrame();
            if (frame is null)
            {
                return;
            }

            var size = frame.ContentSize;
            if (size.Width <= 0 || size.Height <= 0)
            {
                return;
            }

            using var sourceTexture = GetTexture2D(frame.Surface);
            capturedFrame = CopyTextureToMat(sourceTexture, size.Width, size.Height);
        }
        catch
        {
            capturedFrame?.Dispose();
            if (DetectDeviceRemoved())
            {
                OnItemClosed(this, EventArgs.Empty);
            }

            return;
        }

        var handler = FrameReady;
        if (handler is null)
        {
            capturedFrame!.Dispose();
            return;
        }

        handler(this, capturedFrame);
    }

    private void OnItemClosed(object? sender, object e)
    {
        var shouldRaise = false;

        lock (syncRoot)
        {
            if (running)
            {
                StopAndRelease();
                shouldRaise = true;
            }
        }

        if (shouldRaise)
        {
            CaptureEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool DetectDeviceRemoved()
    {
        try
        {
            var reasonCode = device is null ? -1 : device.DeviceRemovedReason.Code;
            return reasonCode != 0;
        }
        catch
        {
            return true;
        }
    }

    private CapturedFrame CopyTextureToMat(ID3D11Texture2D sourceTexture, int width, int height)
    {
        var stagingDescription = sourceTexture.Description;
        stagingDescription.Width = (uint)width;
        stagingDescription.Height = (uint)height;
        stagingDescription.MipLevels = 1;
        stagingDescription.ArraySize = 1;
        stagingDescription.BindFlags = BindFlags.None;
        stagingDescription.CPUAccessFlags = CpuAccessFlags.Read;
        stagingDescription.Usage = ResourceUsage.Staging;
        stagingDescription.MiscFlags = ResourceOptionFlags.None;
        stagingDescription.SampleDescription = new SampleDescription(1, 0);

        using var stagingTexture = device!.CreateTexture2D(stagingDescription);
        context!.CopyResource(stagingTexture, sourceTexture);

        var mapped = context.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            using var bgra = Mat.FromPixelData(height, width, MatType.CV_8UC4, mapped.DataPointer, mapped.RowPitch);
            var bgr = new Mat();
            Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);

            return new CapturedFrame(
                bgr,
                DateTimeOffset.UtcNow,
                Interlocked.Increment(ref sequenceNumber));
        }
        finally
        {
            context.Unmap(stagingTexture, 0);
        }
    }

    private static ID3D11Texture2D GetTexture2D(IDirect3DSurface surface)
    {
        var access = surface.As<IDirect3DDxgiInterfaceAccess>();
        var iid = typeof(ID3D11Texture2D).GUID;
        var texturePointer = access.GetInterface(ref iid);
        return new ID3D11Texture2D(texturePointer);
    }

    private void StopAndRelease()
    {
        if (item is not null)
        {
            item.Closed -= OnItemClosed;
        }

        if (framePool is not null)
        {
            framePool.FrameArrived -= OnFrameArrived;
        }

        session?.Dispose();
        session = null;
        framePool?.Dispose();
        framePool = null;
        item = null;
        running = false;
    }

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        nint GetInterface(ref Guid iid);
    }
}