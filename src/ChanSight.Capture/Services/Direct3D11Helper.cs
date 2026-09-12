using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace ChanSight.Capture.Services;

internal static class Direct3D11Helper
{
    public static IDirect3DDevice CreateDirect3DDevice(nint dxgiDevice)
    {
        var result = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var inspectable);
        Marshal.ThrowExceptionForHR(result);

        try
        {
            return MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        }
        finally
        {
            Marshal.Release(inspectable);
        }
    }

    [DllImport(
        "d3d11.dll",
        EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice",
        ExactSpelling = true,
        PreserveSig = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);
}
