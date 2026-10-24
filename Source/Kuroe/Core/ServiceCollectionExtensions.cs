using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Catalogs.Files;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Node.Llm;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Sessions;
using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Compiler.Description;
using Kuroe.Flow.Runtime;
using Kuroe.Shared;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Tool;
using Kuroe.Tool.Command.Json;
using Kuroe.Tool.Contract;
using Kuroe.Tool.Dotnet;
using Kuroe.Tool.File;
using Kuroe.Tool.Git;
using Kuroe.Tool.KuroeInfo;
using Kuroe.Tool.Json;
using Kuroe.Tool.Time;
using Kuroe.Workflows.BuiltinNodes;
using Kuroe.Workflows.Tasks;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Contract;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Compiler;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.InputChannel;
using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Json;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Shared.StaticGraph.Node;
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

        NodeTypeDescriptionRegistry typeFiles = new();
        InputRegistry inputs = new();
        JsonFlowFormat format = new();
        IFlowExpander expander = new NodeLibraryExpander();
        IFlowDescriptionCompiler descriptionCompiler = new FlowDescriptionCompiler(typeFiles, expander);
        ContainerNodeTypeDescriptions.RegisterInto(typeFiles);
        LlmNodeTypeDescriptions.RegisterInto(typeFiles);
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

        ToolFileRegistry toolFiles = new();
        CommandToolFiles.RegisterInto(toolFiles);

        ErrorOr<IReadOnlyList<ConfiguredTool>> configuredTools =
            ToolFiles.Load(toolFiles, paths.ConfigDirectory, paths.WorkingDirectory);
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
        services.AddSingleton<IFlowRuntimeCatalog>(flows.Value);
        services.AddSingleton<ITaskWorkspace>(taskDirectory.Value);
        services.AddSingleton(descriptionCompiler);
        services.AddSingleton(configuredTools.Value);

        ModelService modelService = new(settings.Value, catalog.Value);
        services.AddSingleton(modelService);

        KuroeComposition composition = new(
            services,
            configuredTools.Value,
            new KuroeStartup(settings.Value.Current.Runtime.LogLevel));

        ExecutionIndex executions = new();
        TaskRegistry taskRegistry = new(executions, taskDirectory.Value.NextTaskNumber);
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
        services.AddSingleton<ToolCollection>();
        services.AddSingleton<ExecutionDispatcher>();

        // 容器只反射 public 构造函数，库内实现类型在此显式建实例，释放仍由容器负责
        services.AddSingleton<IExecutionTargetResolver>(sp => new NodeModelResolver(
            sp.GetRequiredService<IModelAccess>(),
            sp.GetRequiredService<IModelSettings>()));
        services.AddSingleton(sp => new FlowEngine(
            sp.GetRequiredService<ExecutionDispatcher>(),
            sp.GetRequiredService<IExecutionTargetResolver>()));
        services.AddSingleton(sp => new TaskService(
            new TaskActions(
                taskRegistry,
                sp.GetRequiredService<FlowEngine>(),
                sp.GetRequiredService<IExecutionTargetResolver>(),
                sp.GetRequiredService<ITaskWorkspace>()),
            sp.GetRequiredService<IFlowRuntimeCatalog>()));
        services.AddSingleton(sp => new PendingService(
            taskRegistry,
            sp.GetRequiredService<FlowEngine>(),
            sp.GetRequiredService<IInputDelivery>()));
        services.AddSingleton(sp => new SessionFactory(
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<IModelAccess>(),
            sp.GetRequiredService<ToolCollection>()));
        services.AddSingleton<IExecutionRunner>(sp => new ExecutionRunner(
            sp.GetRequiredService<SessionFactory>(),
            sp.GetRequiredService<IExecutionStatusReader>()));

        return composition;
    }

    /// <summary>装配核心工具载体并注册：内置工具、状态查询工具、命令工具与契约工具一次创建，注册前按路径与模型调用名校验重复。
    /// TaskRegistry 是用户任务域，ExecutionIndex 是全部任务域共享的执行索引；契约工具按执行号经执行索引定位执行。</summary>
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

        List<ITool> tools =
        [
            new TimeTool(),
            new ReadFileTool(workingDirectory),
            new ListDirectoryTool(workingDirectory),
            new ReadFilesTool(workingDirectory),
            new WriteFileTool(workingDirectory),
            new PatchFileTool(workingDirectory),
            new MoveFileTool(workingDirectory),
            new GetFileInfoTool(workingDirectory),
            .. GitTools.Tools(workingDirectory),
            .. DotnetTools.Tools(workingDirectory),
            new PortTool(portSubmitter, executions),
            new CannotCompleteTool(cannotCompleteSubmitter),
            new StatusTool(statusReader),
            new TaskKuroeInfoTool(taskRegistry),
            new CatalogKuroeInfoTool(new ToolCatalogView(catalog, models)),
            new FlowKuroeInfoTool(flows),
            new SettingsKuroeInfoTool(settings),
            .. composition.ConfiguredTools.Select(entry => entry.Tool),
        ];

        ErrorOr<Success> added = composition.AddTools(tools);
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

        return Result.Success;
    }
}

