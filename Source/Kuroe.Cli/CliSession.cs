using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Workflows.Flows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Kuroe.Cli;

/// <summary>一次 CLI 调用的服务装配与释放。装配核心服务，日志全部导向 stderr。</summary>
internal sealed class CliSession : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private CliSession(ServiceProvider provider)
    {
        _provider = provider;
        Registry = provider.GetRequiredService<TaskRegistry>();
        Tasks = provider.GetRequiredService<TaskService>();
        Flows = provider.GetRequiredService<FlowService>();
        Catalog = provider.GetRequiredService<CatalogService>();
        Models = provider.GetRequiredService<ModelService>();
        Settings = provider.GetRequiredService<SettingsProvider>();
    }

    /// <summary>用户任务域。</summary>
    public TaskRegistry Registry { get; }

    /// <summary>用户任务的提交与生命周期动作。</summary>
    public TaskService Tasks { get; }

    /// <summary>流程集合。</summary>
    public FlowService Flows { get; }

    /// <summary>提供商与模型目录。</summary>
    public CatalogService Catalog { get; }

    /// <summary>当前模型的选择。</summary>
    public ModelService Models { get; }

    /// <summary>偏好配置。</summary>
    public SettingsProvider Settings { get; }

    /// <summary>装配一次调用的服务。工作目录为空时用进程当前目录，装配失败时返回全部启动错误。</summary>
    public static ErrorOr<CliSession> Create(
        string? workDirectory,
        Action<IServiceCollection>? configure = null)
    {
        ErrorOr<KuroePaths> paths = KuroePaths.Resolve(workDirectory);
        if (paths.IsError)
        {
            return paths.ErrorsOrEmptyList;
        }

        var services = new ServiceCollection();
        ErrorOr<KuroeComposition> composition = services.AddKuroeCore(
            paths.Value,
            new KuroeCoreOptions { AllowFlowFailures = true });
        if (composition.IsError)
        {
            return composition.ErrorsOrEmptyList;
        }

        configure?.Invoke(services);

        services.Configure<ConsoleLoggerOptions>(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        services.AddLogging(builder => builder
            .SetMinimumLevel(composition.Value.Startup.LogLevel)
            .AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
                options.ColorBehavior = LoggerColorBehavior.Disabled;
            }));

        ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        return new CliSession(provider);
    }

    /// <summary>取消全部任务并等待收口，随后释放容器。</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await Tasks.ShutdownAsync().ConfigureAwait(false);
        }
        finally
        {
            await _provider.DisposeAsync().ConfigureAwait(false);
        }
    }
}