using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Catalogs.Files;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Control.Contract;
using Kuroe.Control.Runtime;
using Kuroe.Control.Shared;
using Kuroe.Execution;
using Kuroe.Execution.Shared;
using Kuroe.Files;
using Kuroe.Files.Shared;
using Kuroe.Input.Runtime;
using Kuroe.Input.Shared;
using Kuroe.Node.Llm;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Sessions;
using Kuroe.Node.Llm.Tools;
using Kuroe.Shared;
using Kuroe.Tasks.Persistence.Json;
using Kuroe.Tasks.Persistence.Restoring.Shared;
using Kuroe.Tasks.Persistence.Saving.Shared;
using Kuroe.Tasks.Persistence.Serialization.Shared;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Runtime;
using Kuroe.Tasks.Shared;
using Kuroe.Tool;
using Kuroe.Tool.Command.Json;
using Kuroe.Tool.Contract;
using Kuroe.Tool.Dotnet;
using Kuroe.Tool.File;
using Kuroe.Tool.Git;
using Kuroe.Tool.Json;
using Kuroe.Tool.Time;
using Kuroe.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe;

/// <summary>Kuroe 核心装配入口，只装配中性基础设施与任务运行时。具体流程由各自的装配入口并入。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>装配配置、目录、基础工具、任务运行时与持久化，失败时返回启动期的全部错误。</summary>
    /// <param name="services">服务集合。</param>
    /// <param name="paths">工作目录及其下的配置路径。</param>
    /// <param name="options">核心装配选项。</param>
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

        ToolFileRegistry toolFiles = new();
        CommandToolFiles.RegisterInto(toolFiles);

        ErrorOr<IReadOnlyList<ConfiguredTool>> configuredTools =
            ToolFiles.Load(toolFiles, paths.ConfigDirectory, paths.WorkingDirectory);
        if (configuredTools.IsError)
        {
            return configuredTools.ErrorsOrEmptyList;
        }

        InputChannelRegistry inputs = new();
        KuroeComposition composition = new(
            services,
            configuredTools.Value,
            new KuroeStartup(settings.Value.Current.Runtime.LogLevel),
            files,
            paths,
            coreOptions.PersistTasks,
            inputs);

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

        services.AddSingleton(paths);
        services.AddSingleton(settings.Value);
        services.AddSingleton(catalog.Value);
        services.AddSingleton<IModelAccess>(catalog.Value);
        services.AddSingleton(configuredTools.Value);
        services.AddSingleton<IFlowTemplateCatalog>(sp => new FlowTemplateCatalog(
            sp.GetServices<IFlowTemplateSource>()));

        ModelService modelService = new(settings.Value, catalog.Value);
        services.AddSingleton(modelService);

        ExecutionIndex executions = new();
        TaskRegistry taskRegistry = new(executions, inputs);
        ErrorOr<Success> registered = RegisterTools(
            services,
            composition,
            settings.Value,
            catalog.Value,
            modelService,
            paths.WorkingDirectory,
            taskRegistry,
            executions);
        if (registered.IsError)
        {
            return registered.ErrorsOrEmptyList;
        }

        services.AddSingleton<IToolJson, ToolJson>();
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
        services.AddSingleton<IExecutionIndex>(executions);
        services.AddSingleton<ITaskExecutionGateway>(sp => new TaskExecutionGateway(
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
        services.AddSingleton<IInputChannel>(inputs);
        services.AddSingleton<IInputDelivery>(inputs);
        services.AddSingleton<ITaskIdSource, GuidTaskIdSource>();
        services.AddSingleton(sp => new TaskRecordSerializationMapper([.. sp.GetServices<IFlowRecordCodec>()]));
        services.AddSingleton<ITaskRecordCodec>(sp => new TaskRecordCodec(
            sp.GetRequiredService<TaskRecordSerializationMapper>()));
        services.AddSingleton(sp => new FlowProviderSource(sp.GetServices<FlowProvider>()));
        services.AddSingleton<IFlowProviderSource>(sp => sp.GetRequiredService<FlowProviderSource>());
        services.AddSingleton(sp => new FlowRuntime(
            sp.GetRequiredService<IExecutionRuntime>()));
        services.AddSingleton<IFlowRuntime>(sp => sp.GetRequiredService<FlowRuntime>());
        services.AddSingleton(sp => TaskDomainComposition.Create(
            taskRegistry,
            sp.GetRequiredService<IExecutionRuntime>(),
            sp.GetRequiredService<IFlowRuntime>(),
            sp.GetRequiredService<IFlowProviderSource>(),
            sp.GetRequiredService<ITaskIdSource>(),
            coreOptions.PersistTasks,
            coreOptions.PersistTasks ? paths.RecordsDirectory : null,
            coreOptions.PersistTasks ? sp.GetRequiredService<ITaskRecordCodec>() : null));
        services.AddSingleton(sp => sp.GetRequiredService<TaskDomain>().Pendings);
        if (coreOptions.PersistTasks)
        {
            services.AddSingleton<ITaskRecordCoordinator>(sp =>
                sp.GetRequiredService<TaskDomain>().Records
                ?? throw new InvalidOperationException("任务域未建立写盘协调。"));
            services.AddSingleton<ITaskRestoreService>(sp =>
                sp.GetRequiredService<TaskDomain>().Restore
                ?? throw new InvalidOperationException("任务域未建立恢复服务。"));
        }

        services.AddSingleton(sp => new TaskService(sp.GetRequiredService<TaskDomain>().Actions));
        services.AddSingleton<ITaskControl>(sp => sp.GetRequiredService<TaskService>());
        services.AddSingleton(sp => new SessionFactory(
            sp.GetRequiredService<IModelAccess>(),
            sp.GetRequiredService<IToolFactory>(),
            sp.GetRequiredService<IToolJson>()));
        services.AddSingleton<IExecutionConversationReader, ExecutionConversationReader>();
        services.AddSingleton(sp => new ExecutionRunnerFactory(
            sp.GetRequiredService<SessionFactory>(),
            sp.GetRequiredService<Kuroe.Execution.Shared.IExecutionStatusReader>()));
        services.AddSingleton<Kuroe.Execution.Shared.IExecutionRunnerFactory>(sp =>
            sp.GetRequiredService<ExecutionRunnerFactory>());
        services.AddSingleton<Kuroe.Execution.Shared.IExecutionRestorer>(sp =>
            sp.GetRequiredService<ExecutionRunnerFactory>());

        return composition;
    }

    /// <summary>装配运行期工具源并注册：状态查询工具与契约工具按定义校验重复。</summary>
    private static ErrorOr<Success> RegisterTools(
        IServiceCollection services,
        KuroeComposition composition,
        SettingsProvider settings,
        CatalogService catalog,
        ModelService models,
        string workingDirectory,
        TaskRegistry taskRegistry,
        ExecutionIndex executions)
    {
        TaskSnapshotReader taskReader = new(taskRegistry);
        FlowTemplateCatalog definitionCatalog = new([]);
        KuroeInfoToolSource definitions = new(
            taskReader,
            new ToolCatalogView(catalog, models),
            definitionCatalog,
            settings);

        ErrorOr<Success> added = composition.AddToolDefinitions(definitions.Definitions);
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
        services.AddSingleton<IToolSource>(sp => new KuroeInfoToolSource(
            taskReader,
            new ToolCatalogView(catalog, models),
            sp.GetRequiredService<IFlowTemplateCatalog>(),
            settings));
        services.AddSingleton<IToolFactory>(sp => new KuroeToolFactory(
            [.. sp.GetServices<IToolSource>()],
            workingDirectory));

        return Result.Success;
    }
}