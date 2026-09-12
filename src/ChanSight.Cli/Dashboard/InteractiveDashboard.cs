using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
using ChanSight.Recorder.Services;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace ChanSight.Cli.Dashboard;

public sealed class InteractiveDashboard
{
    private readonly IWindowFinder _windowFinder;
    private readonly IScreenCaptureService _captureService;
    private readonly IVideoRecorder _videoRecorder;
    private readonly DatasetSamplerService _datasetSampler;
    private readonly IGlobalHotKeyService _hotKeyService;
    private readonly ILogger<InteractiveDashboard> _logger;

    private string _status = "IDLE";
    private WindowTarget? _selectedWindow;
    private bool _isRecording;
    private SessionMeta? _currentSession;
    private CancellationTokenSource? _recordingCts;
    private int _targetFps = 30;

    public InteractiveDashboard(
        IWindowFinder windowFinder,
        IScreenCaptureService captureService,
        IVideoRecorder videoRecorder,
        DatasetSamplerService datasetSampler,
        IGlobalHotKeyService hotKeyService,
        ILogger<InteractiveDashboard> logger)
    {
        _windowFinder = windowFinder ?? throw new ArgumentNullException(nameof(windowFinder));
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _videoRecorder = videoRecorder ?? throw new ArgumentNullException(nameof(videoRecorder));
        _datasetSampler = datasetSampler ?? throw new ArgumentNullException(nameof(datasetSampler));
        _hotKeyService = hotKeyService ?? throw new ArgumentNullException(nameof(hotKeyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("ChanSight CLI starting...");

            var windows = await _windowFinder.FindWindowsAsync(
                new WindowSearchOptions(), cancellationToken);

            if (windows.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No game/emulator windows found.[/]");
                AnsiConsole.MarkupLine("[grey]Make sure your emulator (MuMu, LDPlayer, etc.) or game window is running.[/]");
                return;
            }

            if (windows.Count == 1)
            {
                _selectedWindow = windows[0];
                AnsiConsole.MarkupLine($"[green]Auto-selected window:[/] {_selectedWindow.Title}");
            }
            else
            {
                _selectedWindow = await SelectWindowAsync(windows);
                if (_selectedWindow is null)
                    return;
            }

            AnsiConsole.MarkupLine("[yellow]Starting screen capture...[/]");
            await _captureService.StartAsync(_selectedWindow, cancellationToken);
            _logger.LogInformation("Capture started for window: {Title}", _selectedWindow.Title);

            await StartRecordingAsync();
            _logger.LogInformation("Auto-recording started.");

            await RegisterHotKeysAsync(cancellationToken);

            var dashboardCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                dashboardCts.Cancel();
            };

            if (Console.IsOutputRedirected || !AnsiConsole.Profile.Capabilities.Interactive)
            {
                await RunSimpleConsoleLoop(dashboardCts, dashboardCts.Token);
            }
            else
            {
                await AnsiConsole.Live(CreateStatusPanel())
                    .AutoClear(false)
                    .StartAsync(async ctx =>
                    {
                        await RunLiveConsoleLoop(ctx, dashboardCts, dashboardCts.Token);
                    });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Operation cancelled by user.");
        }
        finally
        {
            await ShutdownAsync();
        }
    }

    private async Task RunLiveConsoleLoop(LiveDisplayContext ctx, CancellationTokenSource dashboardCts, CancellationToken cancellationToken)
    {
        var inputTask = RunConsoleInputTask(dashboardCts, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            ctx.UpdateTarget(CreateStatusPanel());

            try
            {
                await Task.Delay(200, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await inputTask;
    }

    private async Task RunSimpleConsoleLoop(CancellationTokenSource dashboardCts, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(CreateStatusPanel());
        AnsiConsole.MarkupLine("[grey]Press R to start/stop recording, S for snapshot, Q to exit.[/]");

        var inputTask = RunConsoleInputTask(dashboardCts, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await inputTask;
    }

    private Task RunConsoleInputTask(CancellationTokenSource dashboardCts, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    switch (key.Key)
                    {
                        case ConsoleKey.Q:
                            dashboardCts.Cancel();
                            return;
                        case ConsoleKey.R:
                            _ = ToggleRecordingAsync();
                            break;
                        case ConsoleKey.S:
                            _ = Task.Run(async () =>
                            {
                                try { await _datasetSampler.TakeManualSnapshotAsync(); }
                                catch (InvalidOperationException) { }
                            });
                            break;
                    }
                }

                try
                {
                    await Task.Delay(100, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, cancellationToken);
    }

    private async Task<WindowTarget?> SelectWindowAsync(IReadOnlyList<WindowTarget> windows)
    {
        var choices = windows
            .Select(w => $"{w.Title} [{w.PhysicalSize.Width}x{w.PhysicalSize.Height}]")
            .ToList();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select a game/emulator window:[/]")
                .PageSize(Math.Min(10, choices.Count))
                .AddChoices(choices));

        var index = choices.IndexOf(selection);
        return index >= 0 ? windows[index] : null;
    }

    private async Task RegisterHotKeysAsync(CancellationToken cancellationToken)
    {
        _hotKeyService.HotKeyPressed += OnHotKeyPressed;

        try
        {
            await _hotKeyService.RegisterAsync(
                new HotKeyDefinition(VirtualKeyCode.F6, HotKeyModifiers.None, "Start/Stop Recording"),
                cancellationToken);
            await _hotKeyService.RegisterAsync(
                new HotKeyDefinition(VirtualKeyCode.F7, HotKeyModifiers.None, "Manual Snapshot"),
                cancellationToken);
            _logger.LogInformation("Hotkeys registered: F6=Start/Stop, F7=Snapshot");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register hotkeys. Use console keys instead.");
        }
    }

    private async void OnHotKeyPressed(object? sender, HotKeyPressedEventArgs e)
    {
        try
        {
            if (e.HotKey.Key == VirtualKeyCode.F6)
            {
                if (_isRecording)
                    await StopRecordingAsync();
                else
                    await StartRecordingAsync();
            }
            else if (e.HotKey.Key == VirtualKeyCode.F7)
            {
                try
                {
                    await _datasetSampler.TakeManualSnapshotAsync();
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling hotkey {HotKey}", e.HotKey.Key);
        }
    }

    private async Task ToggleRecordingAsync()
    {
        try
        {
            if (_isRecording)
                await StopRecordingAsync();
            else
                await StartRecordingAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling recording");
        }
    }

    private async Task StartRecordingAsync()
    {
        if (_selectedWindow is null)
            return;

        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var outputDir = Path.Combine(
            AppContext.BaseDirectory,
            "datasets", "recordings",
            $"{now:yyyyMMdd_HHmmss}_{sessionId.ToString("N")[..8]}");

        _currentSession = new SessionMeta(
            sessionId,
            $"session_{now:yyyyMMddHHmmss}",
            outputDir,
            _selectedWindow,
            now,
            _targetFps,
            _selectedWindow.PhysicalSize);

        _recordingCts = new CancellationTokenSource();

        await _videoRecorder.StartAsync(_currentSession, _captureService.FrameSource, _recordingCts.Token);
        await _datasetSampler.StartAsync(_currentSession, _captureService.FrameSource, _recordingCts.Token);

        _isRecording = true;
        _status = "RECORDING";
        _logger.LogInformation("Recording started: {Name} -> {Dir}", _currentSession.Name, outputDir);
    }

    private async Task StopRecordingAsync()
    {
        if (!_isRecording)
            return;

        _status = "STOPPING";

        _recordingCts?.Cancel();

        await _videoRecorder.StopAsync();
        await _datasetSampler.StopAsync();

        _isRecording = false;
        _status = "IDLE";
        _currentSession = null;
        _recordingCts?.Dispose();
        _recordingCts = null;
        _logger.LogInformation("Recording stopped.");
    }

    private Panel CreateStatusPanel()
    {
        var statusColor = _status switch
        {
            "RECORDING" => "red",
            "PAUSED" => "yellow",
            "STOPPING" => "yellow",
            _ => "grey"
        };

        TimeSpan duration = TimeSpan.Zero;
        if (_isRecording && _currentSession is not null)
            duration = DateTimeOffset.UtcNow - _currentSession.StartedAt;
        else if (_currentSession is not null)
            duration = DateTimeOffset.UtcNow - _currentSession.StartedAt;

        var durationStr = duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss")
            : duration.ToString(@"mm\:ss");

        var resolution = _selectedWindow is not null
            ? $"{_selectedWindow.PhysicalSize.Width}x{_selectedWindow.PhysicalSize.Height}"
            : "N/A";

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn(new GridColumn());

        grid.AddRow(new Text(""), new Text(""));
        grid.AddRow(new Markup("[grey]Status:[/]"), new Markup($"[{statusColor} bold]{_status}[/]"));
        grid.AddRow(new Markup("[grey]Window:[/]"), new Markup(_selectedWindow?.Title ?? "N/A"));
        grid.AddRow(new Markup("[grey]Resolution:[/]"), new Markup(resolution));
        grid.AddRow(new Markup("[grey]Target FPS:[/]"), new Markup($"{_targetFps}"));
        grid.AddRow(new Markup("[grey]Duration:[/]"), new Markup(durationStr));
        grid.AddRow(new Text(""), new Text(""));
        grid.AddRow(new Markup("[yellow]Keys:[/]"), new Markup("[bold]R[/] Start/Stop  [bold]S[/] Snapshot  [bold]Q[/] Exit  [bold]F6/F7[/] Hotkeys"));

        if (_currentSession is not null)
        {
            grid.AddRow(new Text(""), new Text(""));
            grid.AddRow(new Markup("[grey]Output:[/]"), new Markup($"[grey]{_currentSession.OutputDirectory}[/]"));
        }

        return new Panel(grid)
        {
            Header = new PanelHeader("[blue]  ChanSight CLI  [/]", Justify.Center),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };
    }

    private async Task ShutdownAsync()
    {
        _logger.LogInformation("Shutting down...");

        _recordingCts?.Cancel();

        if (_isRecording)
        {
            try
            {
                await _videoRecorder.StopAsync();
                await _datasetSampler.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during recording shutdown");
            }
        }

        try
        {
            await _captureService.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping capture");
        }

        _hotKeyService.HotKeyPressed -= OnHotKeyPressed;

        try
        {
            await _captureService.DisposeAsync();
            await _videoRecorder.DisposeAsync();
            await _datasetSampler.DisposeAsync();
            await _hotKeyService.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during resource disposal");
        }

        _recordingCts?.Dispose();

        AnsiConsole.MarkupLine("[green]ChanSight CLI exited.[/]");
    }
}