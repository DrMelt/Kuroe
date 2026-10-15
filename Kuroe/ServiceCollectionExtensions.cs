using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Catalogs.Files;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Node.Llm;
using Kuroe.Flow.Node.Llm.Runs;
using Kuroe.Flow.Node.Llm.Sessions;
using Kuroe.Flow.Node.Llm.Tools;
using Kuroe.Flow.Execution.Runs;
using Kuroe.Shared;
using Kuroe.Flow.Shared.Execution.Runs;
using Kuroe.Flow.Shared.Execution.Tasks;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Workflows.BuiltinNodes;
using Kuroe.Workflows.Tools.Builtin;
using Kuroe.Workflows.Tools.Command;
using Kuroe.Workflows.Tools.Command.Files;
using Kuroe.Workflows.Tools.Command.Model;
using Kuroe.Workflows.Tools.Files;
using Kuroe.Workflows.Tools.Info;
using Kuroe.Workflows.Tools.Source;
using Kuroe.Workflows.Execution;
using Kuroe.Flow.Execution.Engine;
using Kuroe.Flow.Execution.Tasks;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.StaticGraph.Types;
using Kuroe.Flow.Node.Input;
using Kuroe.Flow.Node.Approval;
using Kuroe.Waiting;
using Kuroe.Workflows.Flows;
using Kuroe.Workflows.Flows.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kuroe;

/// <summary>Kuroe 的装配入口，新增工具或服务改这里。宿主提供路径，日志输出由宿主注册。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>装配配置、目录、流程、工具与任务推进，失败时返回启动期的全部错误。</summary>
    /// <param name="services">服务集合。</param>
    /// <param name="paths">工作目录及其下的配置路径。</param>
    /// <param name="sourceDirectory">随程序导出的源码与文档目录，供 source 工具组查阅。</param>
    /// <returns>启动信息，其中的日志级别用于宿主注册日志输出。</returns>
    public static ErrorOr<KuroeStartup> AddKuroe(this IServiceCollection services, KuroePaths paths, string sourceDirectory)
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

        NodeTypeRegistry nodeTypes = new();
        BuiltinTypeRegistry.RegisterInto(nodeTypes);
        InputNodeTypes.RegisterInto(nodeTypes);
        ApprovalNodeTypes.RegisterInto(nodeTypes);

        ErrorOr<FlowService> flows = FlowService.Create(
            new FlowStore(paths.FlowsFile, paths.WorkingDirectory),
            BuiltinNodes.Nodes,
            nodeTypes,
            () => settings.Value.Current.Runtime.DefaultFlow);
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
        services.AddSingleton<IModelSettings>(settings.Value);
        services.AddSingleton<IModelAccess>(catalog.Value);
        services.AddSingleton(flows.Value);
        services.AddSingleton<IFlowCatalog>(flows.Value);
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
            paths.WorkingDirectory,
            sourceDirectory);
        if (registered.IsError)
        {
            return registered.ErrorsOrEmptyList;
        }

        services.AddSingleton<ToolCollection>();
        services.AddSingleton<IRunPolicy>(sp => new SettingsRunPolicy(sp.GetRequiredService<SettingsProvider>()));
        services.AddSingleton<RunDispatcher>();

        // 容器只反射 public 构造函数，库内实现类型在此显式建实例，释放仍由容器负责
        services.AddSingleton<IExecutionTargetResolver>(sp => new NodeModelResolver(
            sp.GetRequiredService<IModelAccess>(),
            sp.GetRequiredService<IModelSettings>()));
        services.AddSingleton(sp => new FlowEngine(
            sp.GetRequiredService<TaskRegistry>(),
            sp.GetRequiredService<RunDispatcher>(),
            sp.GetRequiredService<IExecutionTargetResolver>()));
        services.AddSingleton(sp => new TaskService(
            sp.GetRequiredService<TaskRegistry>(),
            sp.GetRequiredService<FlowEngine>(),
            sp.GetRequiredService<IFlowCatalog>(),
            sp.GetRequiredService<IExecutionTargetResolver>(),
            nodeTypes));
        services.AddSingleton(sp => new WaitService(
            sp.GetRequiredService<TaskRegistry>(),
            sp.GetRequiredService<FlowEngine>()));
        services.AddSingleton(sp => new DialogueRound(
            sp.GetRequiredService<TaskRegistry>(),
            sp.GetRequiredService<WaitService>()));
        services.AddSingleton(sp => new DialogueHost(
            sp.GetRequiredService<TaskService>(),
            sp.GetRequiredService<DialogueRound>(),
            sp.GetRequiredService<ModelService>(),
            sp.GetRequiredService<TaskRegistry>()));
        services.AddSingleton(sp => new ClientProvider(sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton(sp => new SessionFactory(
            sp.GetRequiredService<ClientProvider>(),
            sp.GetRequiredService<IModelSettings>(),
            sp.GetRequiredService<IModelAccess>(),
            sp.GetRequiredService<ToolCollection>()));
        services.AddSingleton<IRunContract>(sp => new RunReminder(sp.GetRequiredService<TaskRegistry>()));
        services.AddSingleton<IRunExecutor>(sp => new RunExecutor(
            sp.GetRequiredService<SessionFactory>(),
            sp.GetRequiredService<IRunContract>()));

        return new KuroeStartup(settings.Value.Current.Runtime.LogLevel);
    }

    /// <summary>装配工具载体并注册：内置工具、信息查询工具、命令工具与契约工具一次创建，注册前重名校验。
    /// TaskRegistry 与 WorkItemSubmitter 在此建实例并注册，SubmitWorkItemsTool 的提交链与其它消费者共享。</summary>
    private static ErrorOr<Success> RegisterTools(
        IServiceCollection services,
        IReadOnlyList<CommandToolDefinition> definitions,
        SettingsProvider settings,
        CatalogService catalog,
        FlowService flows,
        ModelService models,
        string workingDirectory,
        string sourceDirectory)
    {
        TaskRegistry taskRegistry = new();
        WorkItemSubmitter workItemSubmitter = new(taskRegistry);
        PortSubmitter portSubmitter = new(taskRegistry);
        StatusSubmitter statusSubmitter = new(taskRegistry);

        List<ITool> tools =
        [
            new TimeTool(),
            new FileTool(workingDirectory),
            new SourceTool(sourceDirectory),
            new SubmitWorkItemsTool(workItemSubmitter),
            new PortTool(portSubmitter, taskRegistry),
            new StatusTool(statusSubmitter, taskRegistry),
            new TaskInfoTool(taskRegistry),
            new CatalogInfoTool(catalog, models),
            new FlowInfoTool(flows),
            new SettingsInfoTool(settings),
            new ToolboxInfoTool(definitions),
            .. definitions.Select(definition => (ITool)new CommandTool(definition, workingDirectory)),
        ];

        IReadOnlyList<ToolName> duplicates = [.. tools
            .SelectMany(tool => tool.Functions)
            .GroupBy(function => function.Definition.Name)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];
        if (duplicates.Count > 0)
        {
            return duplicates.Select(name => CommandToolErrors.Duplicate(name.Value)).ToList();
        }

        services.AddSingleton(taskRegistry);
        services.AddSingleton(workItemSubmitter);
        services.AddSingleton(portSubmitter);
        services.AddSingleton(statusSubmitter);
        foreach (ITool tool in tools)
        {
            services.AddSingleton(tool);
        }

        return Result.Success;
    }
}

