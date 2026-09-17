using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ChanSight.Capture.Extensions;
using ChanSight.Capture.Services;
using ChanSight.Core.Annotation;
using ChanSight.Core.Engine;
using ChanSight.Core.Extensions;
using ChanSight.Core.FrameStorage;
using ChanSight.Core.Interfaces;
using ChanSight.Overlay.Services;
using ChanSight.Overlay.ViewModels;
using ChanSight.Overlay.Views;
using ChanSight.Vision.Extensions;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChanSight.Overlay;

public partial class App : Application
{
    public IServiceProvider Services { get; }

    public App()
    {
        Services = ConfigureServices();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddChanSightCore();
        services.AddChanSightVision();
        services.AddChanSightCapture();

        // 捕获服务必须是单例: MainWindow 启动它、LiveRecognitionService 消费它, 二者需同一实例。
        services.AddSingleton<IScreenCaptureService>(static _ => new WgcCaptureService());

        // 关键帧归档: 磁盘实现落本地数据目录。同一实例同时以 FrameArchive 与 IFrameArchive 暴露,
        // 供 ReviewService(读回) 与 LiveRecognitionService(写入) 共享。
        services.AddSingleton<FrameArchive>(_ =>
            new FrameArchive(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ChanSight",
                "frames")));
        services.AddSingleton<IFrameArchive>(static sp => sp.GetRequiredService<FrameArchive>());

        services.AddSingleton<AnnotationStore>();

        // 识别闭包: 把 Vision 的 RecognitionPipeline 适配为可注入/可测的委托 seam。
        services.AddSingleton<LiveRecognitionService.FrameRecognitionFunc>(static sp =>
        {
            var pipeline = sp.GetRequiredService<RecognitionPipeline>();
            return (frame, ct) => pipeline.RecognizeAsync(frame, ct);
        });

        // 重算闭包: 手动重算按钮 → DecisionPanelService.EvaluateAlgorithm。
        services.AddSingleton<Func<GameStateSnapshot, DecisionPanelResult>>(static sp =>
        {
            var decision = sp.GetRequiredService<DecisionPanelService>();
            return decision.EvaluateAlgorithm;
        });

        services.AddSingleton<LiveRecognitionService>();
        services.AddSingleton<LiveViewModel>();
        services.AddSingleton<LiveView>();

        // 回顾链路: ReviewService 读回关键帧 + 识别重算闭包。
        services.AddSingleton<ReviewService.FrameRecognitionFunc>(static sp =>
        {
            var pipeline = sp.GetRequiredService<RecognitionPipeline>();
            return (frame, ct) => pipeline.RecognizeAsync(frame, ct);
        });
        services.AddSingleton<ReviewService>();
        services.AddSingleton<ReviewViewModel>();
        services.AddSingleton<ReviewView>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}