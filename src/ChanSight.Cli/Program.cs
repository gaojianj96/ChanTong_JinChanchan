using ChanSight.Capture.Extensions;
using ChanSight.Cli.Dashboard;
using ChanSight.Core.Extensions;
using ChanSight.Recorder.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using var host = Host
    .CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddChanSightCore();
        services.AddChanSightCapture();
        services.AddChanSightRecorder();
        services.AddSingleton<InteractiveDashboard>();
    })
    .Build();

var dashboard = host.Services.GetRequiredService<InteractiveDashboard>();
await dashboard.RunAsync(CancellationToken.None);