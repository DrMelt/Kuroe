using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Catalogs.Files;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Node.Llm;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Sessions;
using Kuroe.Node.Llm.Tools;
using Kuroe.Node.Command;
using Kuroe.Node.File;
using Kuroe.Flow.Compilation.Description;
using Kuroe.Flow.Persistence.Json;
using Kuroe.Flow.Compilation.Shared.Flow;
using Kuroe.Flow.Runtime;
using Kuroe.Shared;
using Kuroe.Flow.Shared.Flow;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Tool;
using Kuroe.Tool.Command.Json;
using Kuroe.Tool.Contract;
using Kuroe.Tool.Dotnet;
using Kuroe.Tool.File;
using Kuroe.Tool.Git;
using Kuroe.Tool.Json;
using Kuroe.Tool.Time;
using Kuroe.Workflows.BuiltinNodes;
using Kuroe.Workflows.Tasks;
using Kuroe.Flow.Persistence.FileSystem;
using Kuroe.Tools;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Persistence;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Flow.Runtime.Contract;
using Kuroe.Flow.Compilation;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.InputChannel;
using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Json;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Pending;
using Kuroe.Workflows.Flows;
using Kuroe.Workflows.Flows.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kuroe;

/// <summary>Kuroe 核心装配入口，新增核心工具或服务改这里。宿主提供路径，日志输出由宿主注册。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>装配配置、目录、流程、基础工具与任务推进，失败时返回启动期的全部错误。
    /// 内置助手由 AddKuroeAssistant 在核心装配之后并入。</summary>
    /// <param name="services">服务集合。</param>
    /// <param name="paths">工作目录及其下的配置路径。</param>
    /// <param name="options">宿主追加的节点类型与流程库失败策略。</param>
    /// <returns>装配产物，其中的启动信息用于宿主注册日志输出，工具集可继续并入工具。</returns>
    public static ErrorOr<KuroeComposition> AddKuroeCore(
        this IServiceCollection services,
        KuroePaths paths,
        KuroeCoreOptions? options = null)
    {
        KuroeCoreOptions coreOptions = options ?? new KuroeCoreOptions();
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

        ErrorOr<TaskDirectory> taskDirectory = TaskDirectory.Create(paths.TasksDirectory);
        if (taskDirectory.IsError)
        {
            return taskDirectory.ErrorsOrEmptyList;
        }

        ToolFileRegistry toolFiles = new();
        CommandToolFiles.RegisterInto(toolFiles);

        ErrorOr<IReadOnlyList<ConfiguredTool>> configuredTools =
            ToolFiles.Load(toolFiles, paths.ConfigDirectory, paths.WorkingDirectory);
        if (configuredTools.IsError)
        {
            return configuredTools.ErrorsOrEmptyList;
        }

        KuroeComposition composition = new(
            services,
            configuredTools.Value,
            new KuroeStartup(settings.Value.Current.Runtime.LogLevel));

        // 工具定义先于流程编译，供 ToolPorts 在装载期校验变量映射
        List<IToolSource> definitionSources =
        [
            new FileToolSource(),
            new GitToolSource(),
            new DotnetToolSource(),
            new ConfiguredToolSource(configuredTools.Value),
            new TimeToolSource(),
        ];
        ErrorOr<Success> definitionTools = composition.AddToolSources(definitionSources);
        if (definitionTools.IsError)
        {
            return definitionTools.ErrorsOrEmptyList;
        }

        ToolContractCatalog contracts = new(definitionSources.SelectMany(source => source.Definitions));

        NodeTypeDescriptionRegistry typeFiles = new();
        InputRegistry inputs = new();
        JsonFlowFormat format = new();
        IFlowExpander expander = new NodeLibraryExpander();
        IFlowDescriptionCompiler descriptionCompiler = new FlowDescriptionCompiler(typeFiles, expander);
        ContainerNodeTypeDescriptions.RegisterInto(typeFiles);
        LlmNodeTypeDescriptions.RegisterInto(typeFiles, contracts);
        FileNodeTypeDescriptions.RegisterInto(typeFiles, paths.WorkingDirectory);
        CommandNodeTypeDescriptions.RegisterInto(typeFiles, paths.WorkingDirectory);
        coreOptions.RegisterNodeTypes?.Invoke(typeFiles, inputs);

        ErrorOr<FlowService> flows = FlowService.Create(
            new FlowStore(paths.FlowsFile, paths.WorkingDirectory, format, format),
            BuiltinNodes.Nodes,
            descriptionCompiler,
            coreOptions.AllowFlowFailures);
        if (flows.IsError)
        {
            return flows.ErrorsOrEmptyList;
        }

        services.AddSingleton(paths);
        services.AddSingleton(settings.Value);
        services.AddSingleton(catalog.Value);
        services.AddSingleton<IInputChannel>(inputs);
        services.AddSingleton<IInputDelivery>(inputs);
        services.AddSingleton<IModelAccess>(catalog.Value);
        services.AddSingleton(flows.Value);
        services.AddSingleton<IFlowCatalog>(flows.Value);
        services.AddSingleton<IFlowRuntimeCatalog>(flows.Value);
        services.AddSingleton<IFlowSnapshotSource>(flows.Value);
        services.AddSingleton<IFlowSnapshotCompiler>(flows.Value);
        services.AddSingleton<IHostTaskStart>(sp => new KuroeTaskStart(
            paths.WorkingDirectory, taskDirectory.Value, sp.GetRequiredService<ModelService>()));
        services.AddSingleton(descriptionCompiler);
        services.AddSingleton(configuredTools.Value);

        ModelService modelService = new(settings.Value, catalog.Value);
        services.AddSingleton(modelService);

        ExecutionIndex executions = new();
        TaskRegistry taskRegistry = new(executions);
        ErrorOr<Success> registered = RegisterTools(
            services,
            composition,
            settings.Value,
            catalog.Value,
            flows.Value,
            modelService,
            paths.WorkingDirectory,
            taskRegistry,
            executions);
        if (registered.IsError)
        {
            return registered.ErrorsOrEmptyList;
        }

        services.AddSingleton<IToolJson, ToolJson>();
        // 工具面展示用的只读集合，只持有定义，不建立载体
        services.AddSingleton(sp => ToolCollection.ForDisplay(
            sp.GetServices<IToolSource>().SelectMany(source => source.Definitions),
            sp.GetRequiredService<IToolJson>()));
        services.AddSingleton<ExecutionDispatcher>();
        services.AddSingleton<ITaskIdSource, GuidTaskIdSource>();
        services.AddSingleton<ITaskCheckpointCodec>(new TaskCheckpointCodec());
        services.AddSingleton<ITaskCheckpointStore>(sp => new FileTaskCheckpointStore(
            paths.CheckpointsDirectory,
            sp.GetRequiredService<ITaskCheckpointCodec>()));
        services.AddSingleton<ITaskLeaseStore>(new FileTaskLeaseStore(paths.CheckpointsDirectory));
        services.AddSingleton<ITaskRestorer>(new TaskRestorer(taskRegistry));
        services.AddSingleton<ITaskCheckpointSource>(new TaskCheckpointSource(taskRegistry));
        services.AddSingleton<ITaskCheckpointCoordinator>(sp => new TaskCheckpointCoordinator(
            sp.GetRequiredService<ITaskCheckpointSource>(),
            sp.GetRequiredService<ITaskCheckpointStore>()));

        // 容器只反射 public 构造函数，库内实现类型在此显式建实例，释放仍由容器负责
        services.AddSingleton<IExecutionTargetResolver>(sp => new NodeModelResolver(
            sp.GetRequiredService<IModelAccess>()));
        services.AddSingleton(sp => new FlowEngine(
            sp.GetRequiredService<ExecutionDispatcher>(),
            sp.GetRequiredService<IExecutionTargetResolver>()));
        services.AddSingleton<IFlowRunner>(sp => sp.GetRequiredService<FlowEngine>());
        services.AddSingleton<ITaskRestoreRuntime>(sp => new TaskRestoreRuntime(
            taskRegistry,
            sp.GetRequiredService<ITaskRestorer>(),
            sp.GetRequiredService<IFlowRunner>()));
        services.AddSingleton<ITaskRestoreService>(sp => new TaskRestoreService(
            sp.GetRequiredService<ITaskCheckpointStore>(),
            sp.GetRequiredService<IFlowSnapshotCompiler>(),
            sp.GetRequiredService<ITaskLeaseStore>(),
            sp.GetRequiredService<ITaskRestoreRuntime>()));
        services.AddSingleton(sp => new TaskService(
            new TaskActions(
                taskRegistry,
                sp.GetRequiredService<FlowEngine>(),
                sp.GetRequiredService<IExecutionTargetResolver>(),
                sp.GetRequiredService<IHostTaskStart>(),
                sp.GetRequiredService<ITaskIdSource>(),
                coreOptions.PersistTasks ? flows.Value : null,
                coreOptions.PersistTasks ? sp.GetRequiredService<ITaskCheckpointCoordinator>() : null),
            sp.GetRequiredService<IFlowRuntimeCatalog>()));
        services.AddSingleton(sp => new PendingService(
            taskRegistry,
            sp.GetRequiredService<FlowEngine>(),
            sp.GetRequiredService<IInputDelivery>()));
        services.AddSingleton(sp => new SessionFactory(
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<IModelAccess>(),
            sp.GetRequiredService<IToolFactory>(),
            sp.GetRequiredService<IToolJson>()));
        services.AddSingleton<IExecutionRunnerFactory>(sp => new ExecutionRunnerFactory(
            sp.GetRequiredService<SessionFactory>(),
            sp.GetRequiredService<IExecutionStatusReader>()));

        return composition;
    }

    /// <summary>装配运行期工具源并注册：状态查询工具与契约工具按定义校验重复。
    /// 路径型工具定义已在流程编译前注册。载体在每次执行时按上下文创建。TaskRegistry 是用户任务域，ExecutionIndex 是全部任务域共享的执行索引。</summary>
    private static ErrorOr<Success> RegisterTools(
        IServiceCollection services,
        KuroeComposition composition,
        SettingsProvider settings,
        CatalogService catalog,
        FlowService flows,
        ModelService models,
        string workingDirectory,
        TaskRegistry taskRegistry,
        ExecutionIndex executions)
    {
        PortSubmitter portSubmitter = new(executions);
        CannotCompleteSubmitter cannotCompleteSubmitter = new(executions);
        ExecutionStatusReader statusReader = new(executions);

        List<IToolSource> sources =
        [
            new KuroeInfoToolSource(taskRegistry, new ToolCatalogView(catalog, models), flows, settings),
            new ContractToolSource(portSubmitter, cannotCompleteSubmitter, statusReader),
        ];

        ErrorOr<Success> added = composition.AddToolSources(sources);
        if (added.IsError)
        {
            return added.ErrorsOrEmptyList;
        }

        services.AddSingleton(executions);
        services.AddSingleton(taskRegistry);
        services.AddSingleton(portSubmitter);
        services.AddSingleton(cannotCompleteSubmitter);
        services.AddSingleton(statusReader);
        services.AddSingleton<IExecutionStatusReader>(statusReader);
        services.AddSingleton<IToolFactory>(sp => new KuroeToolFactory(
            [.. sp.GetServices<IToolSource>()],
            workingDirectory));

        return Result.Success;
    }
}

