using ErrorOr;
using Kuroe.Host.Composition.Catalogs;
using Kuroe.Host.Composition.Catalogs.Files;
using Kuroe.Host.Composition.Configuration;
using Kuroe.Host.Composition.Configuration.Files;
using Kuroe.Kernel.Control.Runtime;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Kernel.Execution;
using Files;
using Files.Shared;
using Kuroe.Kernel.Input.Runtime;
using Kuroe.Kernel.Input.Shared;
using Kuroe.Host.Ability.Runner.Shared;
using Kuroe.Host.Ability.Runner.Llm;
using Kuroe.Host.Ability.Runner.Llm.Executions;
using Kuroe.Host.Ability.Runner.Llm.Sessions;
using Kuroe.Host.Ability.Runner.Llm.Tools;
using Kuroe.Host.Platform;
using Kuroe.Kernel.Tasks.Persistence.Files;
using Kuroe.Kernel.Tasks.Persistence.Files.Shared;
using Kuroe.Kernel.Tasks.Persistence.Serialization.Json;
using Kuroe.Kernel.Tasks.Persistence.Serialization.Shared;
using Kuroe.Kernel.Tasks.Presentation.Shared;
using Kuroe.Kernel.Tasks.Records.Shared;
using Kuroe.Kernel.Tasks.Runtime;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Host.Ability.Tool;
using Kuroe.Host.Ability.Tool.Implementation.Command.Json;
using Kuroe.Host.Ability.Tool.Implementation.Contract;
using Kuroe.Host.Ability.Tool.Implementation.Dotnet;
using Kuroe.Host.Ability.Tool.Implementation.File;
using Kuroe.Host.Ability.Tool.Implementation.Git;
using Kuroe.Host.Ability.Tool.Json;
using Kuroe.Host.Ability.Tool.Implementation.Time;
using Kuroe.Host.Composition.Tools;
using Microsoft.Extensions.DependencyInjection;
using Kuroe.Kernel.Execution.Shared.Conversation;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Host.Composition.Tasks;

namespace Kuroe.Host.Composition;

/// <summary>Kuroe.Host.Composition 装配入口，只装配中性基础设施与任务运行时。具体流程由各自的装配入口并入。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>装配配置、目录、基础工具、任务运行时与持久化，失败时返回启动期的全部错误。</summary>
    /// <param name="services">服务集合。</param>
    /// <param name="paths">工作目录及其下的配置路径。</param>
    /// <param name="options">装配选项。</param>
    /// <returns>装配产物，其中的启动信息用于宿主注册日志输出，工具集可继续并入工具。</returns>
    public static ErrorOr<KuroeComposition> AddKuroeComposition(
        this IServiceCollection services,
        KuroePaths paths,
        KuroeCompositionOptions? options = null)
    {
        KuroeCompositionOptions coreOptions = options ?? new KuroeCompositionOptions();
        TimeProvider time = coreOptions.Time ?? TimeProvider.System;
        IWorkspaceFiles files = coreOptions.Files ?? new SystemWorkspaceFiles();
        Func<string, ITaskLeaseStore> leaseStores =
            coreOptions.LeaseStores ?? (directory => new FileTaskLeaseStore(directory));
        ErrorOr<SettingsProvider> settings = SettingsProvider.Create(paths, files);
        if (settings.IsError)
        {
            return settings.ErrorsOrEmptyList;
        }

        ErrorOr<CatalogService> catalog = CatalogService.Create(new CatalogStore(paths.CatalogFile, paths.WorkingDirectory, files));
        if (catalog.IsError)
        {
            return catalog.ErrorsOrEmptyList;
        }

        ToolFileRegistry toolFiles = new();
        CommandToolFiles.RegisterInto(toolFiles, files);

        ErrorOr<IReadOnlyList<ConfiguredTool>> configuredTools =
            ToolFiles.Load(toolFiles, paths.ConfigDirectory, paths.WorkingDirectory);
        if (configuredTools.IsError)
        {
            return configuredTools.ErrorsOrEmptyList;
        }

        InputChannelRegistry inputs = new();
        FlowTemplateCatalog flowTemplates = new([]);
        KuroeComposition composition = new(
            services,
            configuredTools.Value,
            new KuroeStartup(settings.Value.Current.Runtime.LogLevel),
            files,
            paths,
            coreOptions.PersistTasks,
            inputs,
            flowTemplates,
            time);

        List<IToolSource> definitionSources =
        [
            new FileToolSource(files),
            new GitToolSource(),
            new DotnetToolSource(),
            new ConfiguredToolSource(configuredTools.Value),
            new TimeToolSource(time),
        ];
        ErrorOr<Success> definitionTools = composition.AddToolSources(definitionSources);
        if (definitionTools.IsError)
        {
            return definitionTools.ErrorsOrEmptyList;
        }

        services.AddSingleton(paths);
        services.AddSingleton(files);
        services.AddSingleton(time);
        services.AddSingleton(leaseStores);
        services.AddSingleton(settings.Value);
        services.AddSingleton(catalog.Value);
        services.AddSingleton<IModelAccess>(catalog.Value);
        services.AddSingleton(configuredTools.Value);
        services.AddSingleton(flowTemplates);
        services.AddSingleton<IFlowTemplateCatalog>(flowTemplates);

        ModelService modelService = new(settings.Value, catalog.Value);
        services.AddSingleton(modelService);

        ExecutionIndex executions = new();
        TaskRegistry taskRegistry = new(executions, inputs);
        ErrorOr<Success> registered = RegisterTools(
            services,
            composition,
            flowTemplates,
            settings.Value,
            catalog.Value,
            modelService,
            taskRegistry,
            executions,
            time);
        if (registered.IsError)
        {
            return registered.ErrorsOrEmptyList;
        }

        ToolJson toolJson = new();
        KuroeToolFactory toolFactory = new(composition.ToolSources, paths.WorkingDirectory);
        SessionFactory sessions = new(catalog.Value, toolFactory, toolJson);
        LlmExecutionRunnerFactory llmRunner = new(sessions, time);
        composition.AttachLlmRunner(llmRunner);

        services.AddSingleton<IToolJson>(toolJson);
        services.AddSingleton<IToolFactory>(toolFactory);
        services.AddSingleton(_ => sessions);
        services.AddSingleton<IExecutionRunnerFactory>(llmRunner);
        services.AddSingleton(sp => ToolCollection.ForDisplay(
            composition.ToolSources.SelectMany(source => source.Definitions),
            toolJson));
        services.AddSingleton<ExecutionDispatcher>();
        services.AddSingleton(sp => new ExecutionRuntime(
            sp.GetRequiredService<ExecutionDispatcher>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IExecutionRuntime>(sp => sp.GetRequiredService<ExecutionRuntime>());
        services.AddSingleton<IExecutionOwnerIndex>(executions);
        services.AddSingleton<IExecutionIndex>(executions);
        services.AddSingleton<ITaskExecutionGateway>(sp => new TaskExecutionGateway(
            executions,
            sp.GetRequiredService<IExecutionRuntime>()));
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
            sp.GetRequiredService<TimeProvider>(),
            coreOptions.PersistTasks,
            coreOptions.PersistTasks ? paths.RecordsDirectory : null,
            coreOptions.PersistTasks ? sp.GetRequiredService<ITaskRecordCodec>() : null,
            files,
            leaseStores));
        services.AddSingleton(sp => sp.GetRequiredService<TaskDomain>().Pendings);
        if (coreOptions.PersistTasks)
        {
            services.AddSingleton(sp =>
                sp.GetRequiredService<TaskDomain>().Records
                ?? throw new InvalidOperationException("任务域未建立写盘协调。"));
            services.AddSingleton(sp =>
                sp.GetRequiredService<TaskDomain>().Restore
                ?? throw new InvalidOperationException("任务域未建立恢复服务。"));
        }

        services.AddSingleton(sp => new TaskService(sp.GetRequiredService<TaskDomain>().Actions));
        services.AddSingleton<ITaskControl>(sp => sp.GetRequiredService<TaskService>());
        services.AddSingleton<IExecutionConversationReader, ExecutionConversationReader>();

        return composition;
    }

    /// <summary>装配运行期工具源并注册：状态查询工具与契约工具按定义校验重复。</summary>
    private static ErrorOr<Success> RegisterTools(
        IServiceCollection services,
        KuroeComposition composition,
        FlowTemplateCatalog flowTemplates,
        SettingsProvider settings,
        CatalogService catalog,
        ModelService models,
        TaskRegistry taskRegistry,
        ExecutionIndex executions,
        TimeProvider time)
    {
        TaskSnapshotReader taskReader = new(taskRegistry);
        KuroeInfoToolSource info = new(
            taskReader,
            new ToolCatalogView(catalog, models),
            flowTemplates,
            settings,
            time);

        ErrorOr<Success> added = composition.AddToolSources([info, new ContractToolSource()]);
        if (added.IsError)
        {
            return added;
        }

        services.AddSingleton(executions);
        services.AddSingleton(taskRegistry);
        services.AddSingleton<ITaskChangeSource>(taskRegistry);
        services.AddSingleton<ITaskReader>(taskReader);

        return Result.Success;
    }
}