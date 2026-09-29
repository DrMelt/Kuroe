using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Executions.Sessions;
using Kuroe.Executions.Tools;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Shared;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Tools;
using Kuroe.Workflows.Engine;
using Kuroe.Workflows.Flows;
using Kuroe.Workflows.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kuroe;

/// <summary>Kuroe 的装配入口，新增工具或服务改这里。宿主提供路径，日志输出由宿主注册。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>装配配置、目录、流程、工具与任务推进，失败时返回启动期的全部错误。</summary>
    /// <returns>启动信息，其中的日志级别用于宿主注册日志输出。</returns>
    public static ErrorOr<KuroeStartup> AddKuroe(this IServiceCollection services, KuroePaths paths)
    {
        ErrorOr<SettingsProvider> settings = SettingsProvider.Create(paths);
        if (settings.IsError)
        {
            return settings.ErrorsOrEmptyList;
        }

        ErrorOr<CatalogService> catalog = CatalogService.Create(new CatalogStore(paths.CatalogFile, paths.WorkingDirectory));
        if (catalog.IsError)
        {
            return catalog.ErrorsOrEmptyList;
        }

        ErrorOr<WorkflowService> flows = WorkflowService.Create(new WorkflowStore(paths.WorkflowFile, paths.WorkingDirectory), settings.Value);
        if (flows.IsError)
        {
            return flows.ErrorsOrEmptyList;
        }

        services.AddSingleton(settings.Value);
        services.AddSingleton(catalog.Value);
        services.AddSingleton(flows.Value);
        services.AddSingleton<ITool>(new TimeTool());
        services.AddSingleton<ITool>(new WeatherTool());
        services.AddSingleton<ITool, PlanTool>();
        services.AddSingleton<ToolCollection>();
        services.AddSingleton<ModelService>();
        services.AddSingleton<TaskRegistry>();
        services.AddSingleton<UnitSubmitter>();
        services.AddSingleton<RunDispatcher>();

        // 容器只反射 public 构造函数，库内实现类型在此显式建实例，释放仍由容器负责
        services.AddSingleton(sp => new NodeModelResolver(
            sp.GetRequiredService<CatalogService>()));
        services.AddSingleton(sp => new WorkflowEngine(
            sp.GetRequiredService<TaskRegistry>(),
            sp.GetRequiredService<RunDispatcher>(),
            sp.GetRequiredService<NodeModelResolver>()));
        services.AddSingleton(sp => new TaskService(
            sp.GetRequiredService<TaskRegistry>(),
            sp.GetRequiredService<WorkflowEngine>(),
            sp.GetRequiredService<WorkflowService>(),
            sp.GetRequiredService<SessionFactory>(),
            sp.GetRequiredService<NodeModelResolver>()));
        services.AddSingleton(sp => new ClientProvider(sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton(sp => new SessionFactory(
            sp.GetRequiredService<ClientProvider>(),
            sp.GetRequiredService<SettingsProvider>(),
            sp.GetRequiredService<CatalogService>(),
            sp.GetRequiredService<ToolCollection>()));
        services.AddSingleton<IRunExecutor>(sp => new RunExecutor(sp.GetRequiredService<SessionFactory>()));

        return new KuroeStartup(settings.Value.Current.Runtime.LogLevel);
    }
}

