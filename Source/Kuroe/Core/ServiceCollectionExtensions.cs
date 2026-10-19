using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Catalogs.Files;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Node.Llm;
using Kuroe.Node.Llm.Json;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Sessions;
using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Runtime;
using Kuroe.Shared;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Tasks;
using Kuroe.Tool;
using Kuroe.Tool.Command;
using Kuroe.Tool.Command.Files;
using Kuroe.Tool.Command.Model;
using Kuroe.Tool.Contract;
using Kuroe.Tool.File;
using Kuroe.Tool.Info;
using Kuroe.Tool.Source;
using Kuroe.Tool.Time;
using Kuroe.Workflows.BuiltinNodes;
using Kuroe.Workflows.Dialogue;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.InputChannel;
using Kuroe.Flow.Json.Parameters;
using Kuroe.Node.Input.Json;
using Kuroe.Node.Approval.Json;
using Kuroe.Pending;
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

        NodeTypeFileRegistry typeFiles = new();
        InputRegistry inputs = new();
        ContainerTypeFiles.RegisterInto(typeFiles);
        LlmTypeFiles.RegisterInto(typeFiles);
        InputTypeFiles.RegisterInto(typeFiles, inputs);
        ApprovalTypeFiles.RegisterInto(typeFiles, inputs);

        ErrorOr<FlowService> flows = FlowService.Create(
            new FlowStore(paths.FlowsFile, paths.WorkingDirectory),
            BuiltinNodes.Nodes,
            typeFiles);
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
        services.AddSingleton<IInputChannel>(inputs);
        services.AddSingleton<IInputDelivery>(inputs);
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

        services.AddSingleton<IToolJson, ToolJson>();
        services.AddSingleton<ToolCollection>();
        services.AddSingleton<IExecutionPolicy>(sp => new SettingsExecutionPolicy(sp.GetRequiredService<SettingsProvider>()));
        services.AddSingleton<ExecutionDispatcher>();

        // 容器只反射 public 构造函数，库内实现类型在此显式建实例，释放仍由容器负责
        services.AddSingleton<IExecutionTargetResolver>(sp => new NodeModelResolver(
            sp.GetRequiredService<IModelAccess>(),
            sp.GetRequiredService<IModelSettings>()));
        services.AddSingleton(sp => new FlowEngine(
            sp.GetRequiredService<TaskRegistry>(),
            sp.GetRequiredService<ExecutionDispatcher>(),
            sp.GetRequiredService<IExecutionTargetResolver>()));
        services.AddSingleton(sp => new TaskService(
            sp.GetRequiredService<TaskRegistry>(),
            sp.GetRequiredService<FlowEngine>(),
            sp.GetRequiredService<IFlowCatalog>(),
            sp.GetRequiredService<IExecutionTargetResolver>()));
        services.AddSingleton(sp => new PendingService(
            sp.GetRequiredService<TaskRegistry>(),
            sp.GetRequiredService<FlowEngine>(),
            sp.GetRequiredService<IInputDelivery>()));
        services.AddSingleton(sp => new DialogueRound(
            sp.GetRequiredService<TaskRegistry>(),
            sp.GetRequiredService<PendingService>()));
        services.AddSingleton(sp => new DialogueHost(
            sp.GetRequiredService<TaskService>(),
            sp.GetRequiredService<DialogueRound>(),
            sp.GetRequiredService<ModelService>(),
            sp.GetRequiredService<TaskRegistry>(),
            sp.GetRequiredService<IInputChannel>()));
        services.AddSingleton(sp => new ClientProvider(sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton(sp => new SessionFactory(
            sp.GetRequiredService<ClientProvider>(),
            sp.GetRequiredService<IModelSettings>(),
            sp.GetRequiredService<IModelAccess>(),
            sp.GetRequiredService<ToolCollection>()));
        services.AddSingleton<IExecutionContract>(sp => new ExecutionReminder(sp.GetRequiredService<TaskRegistry>()));
        services.AddSingleton<IExecutionRunner>(sp => new ExecutionRunner(
            sp.GetRequiredService<SessionFactory>(),
            sp.GetRequiredService<IExecutionContract>()));

        return new KuroeStartup(settings.Value.Current.Runtime.LogLevel);
    }

    /// <summary>装配工具载体并注册：内置工具、信息查询工具、命令工具与契约工具一次创建，注册前按路径校验重复。
    /// TaskRegistry 在此建实例并注册，各契约工具的提交链与其它消费者共享。</summary>
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
        PortSubmitter portSubmitter = new(taskRegistry);
        StatusSubmitter statusSubmitter = new(taskRegistry);

        List<ITool> tools =
        [
            new TimeTool(),
            new ReadFileTool(workingDirectory),
            new ListDirectoryTool(workingDirectory),
            new ReadFilesTool(workingDirectory),
            new WriteFileTool(workingDirectory),
            new PatchFileTool(workingDirectory),
            new GetFileInfoTool(workingDirectory),
            new SourceTool(sourceDirectory),
            new PortTool(portSubmitter, taskRegistry),
            new StatusTool(statusSubmitter, taskRegistry),
            new TaskInfoTool(taskRegistry),
            new CatalogInfoTool(new ToolCatalogView(catalog, models)),
            new FlowInfoTool(flows),
            new SettingsInfoTool(settings),
            new ToolboxInfoTool(DialogueFlow.Tools, definitions),
            .. definitions.Select(definition => (ITool)new CommandTool(definition, workingDirectory)),
        ];

        IReadOnlyList<ToolPath> duplicates = [.. tools
            .SelectMany(tool => tool.Functions)
            .GroupBy(function => function.Definition.Path)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];
        if (duplicates.Count > 0)
        {
            return duplicates.Select(path => CommandToolErrors.Duplicate(path.Value)).ToList();
        }

        services.AddSingleton(taskRegistry);
        services.AddSingleton(portSubmitter);
        services.AddSingleton(statusSubmitter);
        foreach (ITool tool in tools)
        {
            services.AddSingleton(tool);
        }

        return Result.Success;
    }
}

