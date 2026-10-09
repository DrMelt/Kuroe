using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Executions.Sessions;
using Kuroe.Executions.Tools;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Shared;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Tools;
using Kuroe.Tools.CommandTools;
using Kuroe.Tools.KuroeTools;
using Kuroe.Workflows;
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

        ErrorOr<FlowService> flows = FlowService.Create(new FlowStore(paths.FlowsFile, paths.WorkingDirectory), settings.Value);
        if (flows.IsError)
        {
            return flows.ErrorsOrEmptyList;
        }

        ErrorOr<IReadOnlyList<CommandToolDefinition>> configuredTools = CommandToolStore.Load(paths.CommandToolsFile, paths.WorkingDirectory);
        if (configuredTools.IsError)
        {
            return configuredTools.ErrorsOrEmptyList;
        }

        services.AddSingleton(settings.Value);
        services.AddSingleton(catalog.Value);
        services.AddSingleton(flows.Value);
        services.AddSingleton(configuredTools.Value);

        ModelService modelService = new(settings.Value, catalog.Value);
        services.AddSingleton(modelService);

        ErrorOr<Success> registered = RegisterTools(
            services,
            configuredTools.Value,
            settings.Value,
            catalog.Value,
            flows.Value,
            modelService,
            paths.WorkingDirectory);
        if (registered.IsError)
        {
            return registered.ErrorsOrEmptyList;
        }

        services.AddSingleton<ToolCollection>();
        services.AddSingleton<RunDispatcher>();

        // 容器只反射 public 构造函数，库内实现类型在此显式建实例，释放仍由容器负责
        services.AddSingleton(sp => new NodeModelResolver(
            sp.GetRequiredService<CatalogService>(),
            sp.GetRequiredService<SettingsProvider>()));
        services.AddSingleton(sp => new FlowEngine(
            sp.GetRequiredService<TaskRegistry>(),
            sp.GetRequiredService<RunDispatcher>(),
            sp.GetRequiredService<NodeModelResolver>()));
        services.AddSingleton(sp => new TaskService(
            sp.GetRequiredService<TaskRegistry>(),
            sp.GetRequiredService<FlowEngine>(),
            sp.GetRequiredService<FlowService>(),
            sp.GetRequiredService<NodeModelResolver>()));
        services.AddSingleton(sp => new DialogueHost(
            sp.GetRequiredService<TaskService>(),
            sp.GetRequiredService<ModelService>(),
            sp.GetRequiredService<TaskRegistry>()));
        services.AddSingleton(sp => new ClientProvider(sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton(sp => new SessionFactory(
            sp.GetRequiredService<ClientProvider>(),
            sp.GetRequiredService<SettingsProvider>(),
            sp.GetRequiredService<CatalogService>(),
            sp.GetRequiredService<ToolCollection>()));
        services.AddSingleton<IRunExecutor>(sp => new RunExecutor(sp.GetRequiredService<SessionFactory>()));

        return new KuroeStartup(settings.Value.Current.Runtime.LogLevel);
    }

    /// <summary>装配工具载体并注册：内置工具、信息查询工具、命令工具与契约工具一次创建，注册前重名校验。
    /// TaskRegistry 与 PlanSubmitter 在此建实例并注册，PlanTool 的提交链与其它消费者共享。</summary>
    private static ErrorOr<Success> RegisterTools(
        IServiceCollection services,
        IReadOnlyList<CommandToolDefinition> definitions,
        SettingsProvider settings,
        CatalogService catalog,
        FlowService flows,
        ModelService models,
        string workingDirectory)
    {
        TaskRegistry taskRegistry = new();
        PlanSubmitter planSubmitter = new(taskRegistry);
        PortSubmitter portSubmitter = new(taskRegistry);

        List<ITool> tools =
        [
            new TimeTool(),
            new FileTool(workingDirectory),
            new PlanTool(planSubmitter),
            new PortTool(portSubmitter, taskRegistry),
            new TaskInfoTool(taskRegistry),
            new CatalogInfoTool(catalog, models),
            new FlowInfoTool(flows),
            new SettingsInfoTool(settings),
            new ToolboxInfoTool(definitions),
            .. definitions.Select(definition => (ITool)new CommandTool(definition, workingDirectory)),
        ];

        IReadOnlyList<ToolName> duplicates = [.. tools
            .SelectMany(tool => tool.Functions)
            .GroupBy(function => function.Name)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];
        if (duplicates.Count > 0)
        {
            return duplicates.Select(name => CommandToolErrors.Duplicate(name.Value)).ToList();
        }

        services.AddSingleton(taskRegistry);
        services.AddSingleton(planSubmitter);
        services.AddSingleton(portSubmitter);
        foreach (ITool tool in tools)
        {
            services.AddSingleton<ITool>(tool);
        }

        return Result.Success;
    }
}

