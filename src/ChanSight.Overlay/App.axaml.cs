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
using ChanSight.Core.Season;
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

        // 赛季字典: 覆盖 Core 中 AppContext.BaseDirectory/data/season 的默认路径, 落 %LOCALAPPDATA%/ChanSight/season,
        // 使 seeder/自学习/回顾确认在同一份持久化数据上读写。last-registered-wins, 三个依赖均指向同一 store。
        services.AddSingleton(static _ =>
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ChanSight",
                "season");
            var seeds = new Dictionary<string, SeasonDictionary>(StringComparer.Ordinal)
            {
                [SeasonDictionarySeed.DefaultSeasonId] = SeasonDictionarySeed.DefaultDictionary,
            };
            return new SeasonDictionaryStore(root, seeds);
        });
        services.AddSingleton<ISeasonDictionaryReader>(static sp => sp.GetRequiredService<SeasonDictionaryStore>());
        services.AddSingleton<ISeasonDictionaryWriter>(static sp => sp.GetRequiredService<SeasonDictionaryStore>());
        services.AddSingleton<SeasonRuntime>();
        services.AddSingleton<SeasonRegistry>();

        // 网络搜索字典 seeder: 产候选, 供人工审核后合并(不直接进正式字典)。
        services.AddSingleton<DictionarySeederService>();

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

        // LLM 建议闭包: 手动 LLM 建议按钮 → DecisionPanelService.EvaluateWithAdvisorAsync。
        services.AddSingleton<Func<GameStateSnapshot, AdvisorEvent, CancellationToken, Task<DecisionPanelResult>>>(static sp =>
        {
            var decision = sp.GetRequiredService<DecisionPanelService>();
            return decision.EvaluateWithAdvisorAsync;
        });

        services.AddSingleton<LiveRecognitionService>();
        services.AddSingleton<LiveViewModel>();

        // 手动整帧 VLM 识别闭包: 从 LatestFrameStore 取当前帧整图, 走 ManualFrameVlmService。
        services.AddSingleton<LatestFrameStore>();
        services.AddSingleton<ManualRecognizeFunc>(static sp =>
        {
            var manual = sp.GetRequiredService<ManualFrameVlmService>();
            var frames = sp.GetRequiredService<LatestFrameStore>();
            return (isSelf, ct) =>
            {
                var frame = frames.TakeLatest();
                if (frame is null)
                {
                    return Task.FromException<RecognitionFrame>(
                        new InvalidOperationException("尚无可用画面, 请先开始捕获。"));
                }

                try
                {
                    return manual.RecognizeAsync(frame, isSelf, ct);
                }
                finally
                {
                    frame.Dispose();
                }
            };
        });
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

        // 回放重模拟链路: ReplaySimService 读回关键帧 + 直读识别缓存 + 手动触发 VLM 重识别。
        services.AddSingleton<ReplaySimService>();
        services.AddSingleton<ReplayViewModel>();
        services.AddSingleton<ReplayView>();

        // 字典查看: 读 SeasonRuntime 字典 + 候选 + VLM prompt + MetaInfoStore 推荐阵容。
        // meta 数据目录与 season/frames 同根, 落 %LOCALAPPDATA%/ChanSight/meta(如 {root}/meta/S18)。
        services.AddSingleton<DictionaryViewModel>(static sp =>
        {
            var runtime = sp.GetRequiredService<SeasonRuntime>();
            var writer = sp.GetRequiredService<ISeasonDictionaryWriter>();
            var manual = sp.GetRequiredService<ManualFrameVlmService>();
            var registry = sp.GetRequiredService<SeasonRegistry>();
            var metaRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ChanSight",
                "meta");
            return new DictionaryViewModel(runtime, writer, manual.BuildPromptForDisplay, registry, metaRoot);
        });
        services.AddSingleton<DictionaryView>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}