using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Catalogs.Files;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Control.Graph.Runtime.Catalog;
using Kuroe.Flow.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Flow.Graph.Records.Shared;
using Kuroe.Flow.Tasks.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;
using Kuroe.Node.Llm;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Sessions;
using Kuroe.Node.Llm.Tools;
using Kuroe.Node.Command;
using Kuroe.Node.File;
using Kuroe.Flow.Compilation.Description;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Persistence.Json;
using Kuroe.Flow.Persistence.Code;
using Kuroe.Flow.Persistence.Graph;
using Kuroe.Flow.Persistence.Serialization.Shared;
using Kuroe.Flow.Compilation.Shared.Flow;
using Kuroe.Execution;
using Kuroe.Flow.Runtime;
using Kuroe.Shared;
using Kuroe.Flow.Persistence.Restoring.Shared;
using Kuroe.Flow.Persistence.Saving.Shared;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
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
using Kuroe.Flow.Persistence.Files;
using Kuroe.Tools;
using Kuroe.Flow.Control.Graph.Runtime.Engine;
using Kuroe.Flow.Control.Contract;
using Kuroe.Flow.Persistence.Restoring;
using Kuroe.Flow.Persistence.Saving;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Flow.Compilation;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Files;
using Kuroe.Files.Shared;
using Kuroe.Flow.InputChannel;
using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Json;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Pending;
using Kuroe.Workflows.Flows;
using Kuroe.Workflows.Flows.Files;
using Microsoft.Extensions.DependencyInjection;

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
        IWorkspaceFiles files = SystemWorkspaceFiles.Instance;
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

        // 宿主开始输入表只承载流程名到开始端口的映射，与流程开始入口的一致性在任务提交时校验
        ErrorOr<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> hostForms =
            new HostStartInputsStore(paths.HostStartInputsFile).Load();
        if (hostForms.IsError)
        {
            return hostForms.ErrorsOrEmptyList;
        }

        ErrorOr<HostStartInputs> hostStartInputs = HostStartInputs.Create(hostForms.Value, StartSources.Supported);
        if (hostStartInputs.IsError)
        {
            return hostStartInputs.ErrorsOrEmptyList;
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
            new KuroeStartup(settings.Value.Current.Runtime.LogLevel),
            files);

        // 工具定义先于流程编译，供 ToolPorts 在装载期校验变量映射
        List<IToolSource> definitionSources =
        [
            new FileToolSource(files),
            new GitToolSource(),
            new DotnetToolSource(),
            new ConfiguredToolSource(configuredTools.Value),
            new TimeToolSource(TimeProvider.System),
        ];
        ErrorOr<Success> definitionTools = composition.AddToolSources(definitionSources);
        if (definitionTools.IsError)
        {
            return definitionTools.ErrorsOrEmptyList;
        }

        ToolContractCatalog contracts = new(definitionSources.SelectMany(source => source.Definitions));

        NodeTypeDescriptionRegistry typeFiles = new();
        InputRegistry inputs = new();
        JsonNodeFormat nodeFormat = new();
        JsonFlowFormat flowFormat = new();
        JsonFlowImportReader importFormat = new(nodeFormat, flowFormat);
        IFlowExpander expander = new NodeLibraryExpander();
        IFlowDescriptionCompiler descriptionCompiler = new FlowDescriptionCompiler(typeFiles, expander);
        ContainerNodeTypeDescriptions.RegisterInto(typeFiles);
        LlmNodeTypeDescriptions.RegisterInto(typeFiles, contracts);
        FileNodeTypeDescriptions.RegisterInto(typeFiles, paths.WorkingDirectory, files);
        CommandNodeTypeDescriptions.RegisterInto(typeFiles, paths.WorkingDirectory, coreOptions.CommandNodeProcessRunner);
        coreOptions.RegisterNodeTypes?.Invoke(typeFiles, inputs);

        ErrorOr<FlowService> flows = FlowService.Create(
            new FlowStore(
                paths.NodesFile,
                paths.FlowsFile,
                paths.WorkingDirectory,
                nodeFormat,
                nodeFormat,
                flowFormat,
                flowFormat,
                importFormat),
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
        services.AddSingleton<IFlowLibrary>(flows.Value);
        services.AddSingleton<IFlowRuntimeCatalog>(flows.Value);
        services.AddSingleton<IFlowSnapshotSource>(flows.Value);
        services.AddSingleton<IFlowSnapshotCompiler>(flows.Value);
        services.AddSingleton(hostStartInputs.Value);
        services.AddSingleton<IHostStartInputs>(hostStartInputs.Value);
        services.AddSingleton<IFlowTemplateCatalog>(sp => new FlowTemplateCatalog(
            sp.GetRequiredService<IFlowProviderSource>()));
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
            hostStartInputs.Value,
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
        services.AddSingleton(sp => new ExecutionRunnerRegistry(
            [.. sp.GetServices<Kuroe.Execution.Shared.IExecutionRunnerFactory>()],
            sp.GetService<Kuroe.Execution.Shared.IExecutionRunnerFactory>()));
        services.AddSingleton(sp => new ExecutionRestorerRegistry(
            [.. sp.GetServices<Kuroe.Execution.Shared.IExecutionRestorer>()]));
        services.AddSingleton(sp => new ExecutionRuntime(
            sp.GetRequiredService<ExecutionDispatcher>(),
            sp.GetRequiredService<ExecutionRunnerRegistry>(),
            sp.GetRequiredService<ExecutionRestorerRegistry>()));
        services.AddSingleton<IExecutionRuntime>(sp => sp.GetRequiredService<ExecutionRuntime>());
        services.AddSingleton<IExecutionOwnerIndex>(executions);
        services.AddSingleton<IFlowExecutionGateway>(sp => new FlowExecutionGateway(
            executions,
            sp.GetRequiredService<IExecutionRuntime>()));
        services.AddSingleton<IExecutionContractWriter, ExecutionContractWriter>();
        services.AddSingleton(sp => new PortSubmitter(
            sp.GetRequiredService<IExecutionOwnerIndex>(),
            sp.GetRequiredService<IExecutionContractWriter>()));
        services.AddSingleton<IExecutionPortSubmitter>(sp => sp.GetRequiredService<PortSubmitter>());
        services.AddSingleton(sp => new CannotCompleteSubmitter(
            sp.GetRequiredService<IExecutionOwnerIndex>(),
            sp.GetRequiredService<IExecutionContractWriter>()));
        services.AddSingleton<IExecutionCannotCompleteSubmitter>(sp => sp.GetRequiredService<CannotCompleteSubmitter>());
        services.AddSingleton(sp => new ExecutionStatusReader(
            sp.GetRequiredService<IExecutionOwnerIndex>()));
        services.AddSingleton<IExecutionStatusReader>(sp => sp.GetRequiredService<ExecutionStatusReader>());
        services.AddSingleton<IToolSource>(sp => new ContractToolSource(
            sp.GetRequiredService<IExecutionPortSubmitter>(),
            sp.GetRequiredService<IExecutionCannotCompleteSubmitter>(),
            sp.GetRequiredService<IExecutionStatusReader>()));
        services.AddSingleton<ITaskIdSource, GuidTaskIdSource>();
        services.AddSingleton<IFlowRecordCodec, GraphFlowRecordCodec>();
        services.AddSingleton<IFlowRecordCodec, CodeFlowRecordCodec>();
        services.AddSingleton(sp => new TaskRecordSerializationMapper([.. sp.GetServices<IFlowRecordCodec>()]));
        services.AddSingleton<ITaskRecordCodec>(sp => new TaskRecordCodec(
            sp.GetRequiredService<TaskRecordSerializationMapper>()));
        services.AddSingleton<ITaskRecordStore>(sp => new FileTaskRecordStore(
            paths.RecordsDirectory,
            sp.GetRequiredService<ITaskRecordCodec>()));
        services.AddSingleton<ITaskLeaseStore>(sp => new FileTaskLeaseStore(paths.RecordsDirectory));
        services.AddSingleton<ITaskRecordSource>(new TaskRecordSource(taskRegistry));
        services.AddSingleton<ITaskRecordCoordinator>(sp => new TaskRecordCoordinator(
            sp.GetRequiredService<ITaskRecordSource>(),
            sp.GetRequiredService<ITaskRecordStore>()));

        // 容器只反射 public 构造函数，库内实现类型在此显式建实例，释放仍由容器负责
        services.AddSingleton<IExecutionTargetResolver>(sp => new ExecutionTargetResolver(
            new NodeModelResolver(sp.GetRequiredService<IModelAccess>())));
        services.AddSingleton(sp => new FlowProvider(
            new GraphFlowIdentity(flows.Value),
            new GraphFlowSubmission(
                flows.Value,
                flows.Value,
                hostStartInputs.Value,
                sp.GetRequiredService<IHostTaskStart>(),
                sp.GetRequiredService<IExecutionTargetResolver>(),
                sp.GetRequiredService<IFlowExecutionGateway>(),
                coreOptions.PersistTasks ? flows.Value : null),
            new GraphFlowStateBinder(),
            new GraphFlowControlFactory(
                sp.GetRequiredService<IExecutionTargetResolver>(),
                inputs,
                sp.GetRequiredService<IFlowExecutionGateway>()),
            new GraphFlowPresentation(),
            new GraphFlowRecordSource(),
            new GraphFlowTemplateSource(
                flows.Value,
                hostStartInputs.Value)));
        services.AddSingleton(sp => new FlowProviderSource(sp.GetServices<FlowProvider>()));
        services.AddSingleton<IFlowProviderSource>(sp => sp.GetRequiredService<FlowProviderSource>());
        services.AddSingleton(sp => new FlowRuntime(
            sp.GetRequiredService<IExecutionRuntime>()));
        services.AddSingleton<IFlowRuntime>(sp => sp.GetRequiredService<FlowRuntime>());
        services.AddSingleton<ITaskRestoreRuntime>(sp => new TaskRestoreRuntime(
            taskRegistry,
            sp.GetRequiredService<IExecutionRuntime>(),
            sp.GetRequiredService<IFlowProviderSource>(),
            sp.GetRequiredService<IFlowRuntime>()));
        if (coreOptions.PersistTasks)
        {
            services.AddSingleton<ITaskRestoreService>(sp => new TaskRestoreService(
                sp.GetRequiredService<ITaskRecordStore>(),
                sp.GetRequiredService<ITaskLeaseStore>(),
                sp.GetRequiredService<ITaskRestoreRuntime>()));
        }
        services.AddSingleton(sp => new TaskService(
            new TaskActions(
                taskRegistry,
                sp.GetRequiredService<IExecutionRuntime>(),
                sp.GetRequiredService<IFlowRuntime>(),
                sp.GetRequiredService<IFlowProviderSource>(),
                sp.GetRequiredService<ITaskIdSource>(),
                coreOptions.PersistTasks ? sp.GetRequiredService<ITaskRecordCoordinator>() : null,
                coreOptions.PersistTasks ? sp.GetRequiredService<ITaskLeaseStore>() : null)));
        services.AddSingleton<ITaskControl>(sp => sp.GetRequiredService<TaskService>());
        services.AddSingleton(sp => new PendingService(
            taskRegistry,
            sp.GetRequiredService<IFlowRuntime>()));
        services.AddSingleton(sp => new SessionFactory(
            sp.GetRequiredService<IModelAccess>(),
            sp.GetRequiredService<IToolFactory>(),
            sp.GetRequiredService<IToolJson>()));
        services.AddSingleton(sp => new ExecutionRunnerFactory(
            sp.GetRequiredService<SessionFactory>(),
            sp.GetRequiredService<Kuroe.Execution.Shared.IExecutionStatusReader>()));
        services.AddSingleton<Kuroe.Execution.Shared.IExecutionRunnerFactory>(sp =>
            sp.GetRequiredService<ExecutionRunnerFactory>());
        services.AddSingleton<Kuroe.Execution.Shared.IExecutionRestorer>(sp =>
            sp.GetRequiredService<ExecutionRunnerFactory>());

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
        HostStartInputs hostStartInputs,
        ModelService models,
        string workingDirectory,
        TaskRegistry taskRegistry,
        ExecutionIndex executions)
    {
        TaskSnapshotReader taskReader = new(taskRegistry);

        List<IToolSource> sources =
        [
            new KuroeInfoToolSource(
                taskReader,
                new ToolCatalogView(catalog, models),
                new FlowTemplateCatalog(new GraphFlowTemplateSource(flows, hostStartInputs)),
                settings),
        ];

        ErrorOr<Success> added = composition.AddToolSources(sources);
        if (added.IsError)
        {
            return added.ErrorsOrEmptyList;
        }

        ErrorOr<Success> contractDefinitions = composition.AddToolDefinitions(ContractToolSource.AllDefinitions);
        if (contractDefinitions.IsError)
        {
            return contractDefinitions.ErrorsOrEmptyList;
        }

        services.AddSingleton(executions);
        services.AddSingleton(taskRegistry);
        services.AddSingleton<ITaskChangeSource>(taskRegistry);
        services.AddSingleton<ITaskReader>(taskReader);
        services.AddSingleton<IToolFactory>(sp => new KuroeToolFactory(
            [.. sp.GetServices<IToolSource>()],
            workingDirectory));

        return Result.Success;
    }
}

