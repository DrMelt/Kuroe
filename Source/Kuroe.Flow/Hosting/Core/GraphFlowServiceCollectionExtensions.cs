using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration.Files;
using Kuroe.Control.Shared;
using Kuroe.Execution;
using Kuroe.Flow.Compilation;
using Kuroe.Flow.Compilation.Description;
using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Compilation.Shared.Flow;
using Kuroe.Flow.Control.Graph.Runtime.Catalog;
using Kuroe.Flow.Control.Graph.Runtime.Engine;
using Kuroe.Flow.Graph.Records.Shared;
using BuiltinNodeCatalog = Kuroe.Flow.Hosting.Workflows.BuiltinNodes.BuiltinNodes;
using Kuroe.Flow.Hosting.Workflows.Flows;
using Kuroe.Flow.Hosting.Workflows.Flows.Files;
using Kuroe.Flow.Hosting.Workflows.Tasks;
using Kuroe.Flow.Json;
using Kuroe.Input.Shared;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Persistence.Graph;
using Kuroe.Node.Approval;
using Kuroe.Node.Command;
using Kuroe.Node.Input;
using Kuroe.Node.File;
using Kuroe.Node.Llm;
using Kuroe.Process;
using Kuroe.Runner;
using Kuroe.Runner.Approval;
using Kuroe.Runner.Command;
using Kuroe.Runner.File;
using Kuroe.Runner.Input;
using Kuroe.Tool;
using Kuroe.Tasks.Records.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Flow.Hosting;

/// <summary>图流程装配选项：节点类型、流程库失败策略与命令节点的进程边界。</summary>
public sealed record GraphFlowOptions
{
    /// <summary>追加节点类型描述。图流程固定注册 Container、Llm、FileRead、FileWrite 与 Command。</summary>
    public Action<INodeTypeDescriptionRegistry>? RegisterNodeTypes { get; init; }

    /// <summary>流程库个别流程编译失败时是否继续装配。为 false 时按启动错误返回。</summary>
    public bool AllowFlowFailures { get; init; }

    /// <summary>命令节点的进程执行边界，未给出时使用系统进程。命令工具使用自己的进程边界。</summary>
    public IProcessRunner? CommandNodeProcessRunner { get; init; }
}

/// <summary>图流程装配入口：注册流程库、节点类型、图流程实现与图记录编解码。</summary>
public static class GraphFlowServiceCollectionExtensions
{
    /// <summary>在核心装配之后并入图流程。核心不引用图流程实现。</summary>
    public static ErrorOr<Success> AddKuroeGraphFlows(
        this IServiceCollection services,
        KuroeComposition composition,
        GraphFlowOptions? options = null)
    {
        GraphFlowOptions graphOptions = options ?? new GraphFlowOptions();
        KuroePaths paths = composition.Paths;

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

        ErrorOr<TaskDirectory> taskDirectory = TaskDirectory.Create(paths.TasksDirectory);
        if (taskDirectory.IsError)
        {
            return taskDirectory.ErrorsOrEmptyList;
        }

        ToolContractCatalog contracts = new(composition.ToolDefinitions);
        NodeTypeDescriptionRegistry typeFiles = new();
        JsonNodeFormat nodeFormat = new();
        JsonFlowFormat flowFormat = new();
        JsonFlowImportReader importFormat = new(nodeFormat, flowFormat);
        IFlowExpander expander = new NodeLibraryExpander();
        IFlowDescriptionCompiler descriptionCompiler = new FlowDescriptionCompiler(typeFiles, expander);
        ContainerNodeTypeDescriptions.RegisterInto(typeFiles);
        LlmNodeTypeDescriptions.RegisterInto(typeFiles, contracts);
        FileNodeTypeDescriptions.RegisterInto(typeFiles, paths.WorkingDirectory);
        CommandNodeTypeDescriptions.RegisterInto(typeFiles, paths.WorkingDirectory);
        graphOptions.RegisterNodeTypes?.Invoke(typeFiles);

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
            BuiltinNodeCatalog.Nodes,
            descriptionCompiler,
            graphOptions.AllowFlowFailures);
        if (flows.IsError)
        {
            return flows.ErrorsOrEmptyList;
        }
        services.AddSingleton(flows.Value);
        services.AddSingleton<IFlowCatalog>(flows.Value);
        services.AddSingleton<IFlowLibrary>(flows.Value);
        services.AddSingleton<IFlowRuntimeCatalog>(flows.Value);
        services.AddSingleton<IFlowSnapshotSource>(flows.Value);
        services.AddSingleton<IFlowSnapshotCompiler>(flows.Value);
        services.AddSingleton(hostStartInputs.Value);
        services.AddSingleton<IHostStartInputs>(hostStartInputs.Value);
        services.AddSingleton<IHostTaskStart>(sp => new KuroeTaskStart(
            paths.WorkingDirectory, taskDirectory.Value, sp.GetRequiredService<ModelService>()));
        services.AddSingleton(descriptionCompiler);
        services.AddSingleton<IFlowRecordCodec, GraphFlowRecordCodec>();
        services.AddSingleton<IExecutionTargetResolver>(sp => new ExecutionTargetResolver(
            new NodeModelResolver(sp.GetRequiredService<IModelAccess>())));
        services.AddSingleton<IFlowTemplateSource>(new GraphFlowTemplateSource(
            flows.Value, hostStartInputs.Value));
        services.AddSingleton(sp => new FlowProvider(
            new GraphFlowIdentity(flows.Value),
            new GraphFlowSubmission(
                flows.Value,
                flows.Value,
                hostStartInputs.Value,
                sp.GetRequiredService<IHostTaskStart>(),
                sp.GetRequiredService<IExecutionTargetResolver>(),
                sp.GetRequiredService<ITaskExecutionGateway>(),
                composition.PersistTasks ? flows.Value : null),
            new GraphFlowStateBinder(),
            new GraphFlowControlFactory(
                sp.GetRequiredService<IExecutionTargetResolver>(),
                sp.GetRequiredService<ITaskExecutionGateway>()),
            new GraphFlowPresentation(),
            new GraphFlowRecordSource()));

        AddBuiltinGraphExecutionRunners(services, composition, graphOptions);

        return Result.Success;
    }

    /// <summary>注册图节点运行器。执行器工厂类型由节点类型给出，运行器实现不认识节点。</summary>
    public static void AddBuiltinGraphExecutionRunners(
        this IServiceCollection services,
        KuroeComposition composition,
        GraphFlowOptions? options = null)
    {
        GraphFlowOptions graphOptions = options ?? new GraphFlowOptions();
        IProcessRunner process = graphOptions.CommandNodeProcessRunner ?? new ProcessRunner();
        IInputChannel channel = composition.InputChannel;

        services.AddSingleton(new ExecutionRunnerRegistration(
            CommandNodeTypes.RunnerFactoryType,
            new CommandExecutionRunnerFactory(process)));
        services.AddSingleton(new ExecutionRunnerRegistration(
            FileReadNodeTypes.RunnerFactoryType,
            new FileReadExecutionRunnerFactory(composition.Files)));
        services.AddSingleton(new ExecutionRunnerRegistration(
            FileWriteNodeTypes.RunnerFactoryType,
            new FileWriteExecutionRunnerFactory(composition.Files)));
        services.AddSingleton(new ExecutionRunnerRegistration(
            InputNodeTypes.RunnerFactoryType,
            new InputExecutionRunnerFactory(channel)));
        services.AddSingleton(new ExecutionRunnerRegistration(
            ApprovalNodeTypes.RunnerFactoryType,
            new ApprovalExecutionRunnerFactory(channel)));
    }
}