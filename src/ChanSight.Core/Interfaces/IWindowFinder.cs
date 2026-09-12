using ChanSight.Core.Models;

namespace ChanSight.Core.Interfaces;

public interface IWindowFinder
{
    ValueTask<IReadOnlyList<WindowTarget>> FindWindowsAsync(
        WindowSearchOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<WindowTarget?> GetWindowTargetAsync(nint hwnd, CancellationToken cancellationToken = default);
}
