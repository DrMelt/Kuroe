using ErrorOr;
using Kuroe.Composition;
using Kuroe.Composition.Catalogs;
using Kuroe.Composition.Configuration.Files;
using Kuroe.Kernel.Control.Shared;
using Kuroe.FlowDescription.Compilation;
using Kuroe.FlowDescription.Compilation.NodeTypes;
using Kuroe.FlowDescription.Compilation.Shared;
using Kuroe.FlowDescription.Compilation.Shared.Flow;
using Kuroe.FlowDescription.Compilation.Shared.NodeTypes;
using Kuroe.Flow.Compilation;
using Kuroe.Flow.Control.Runtime.Catalog;
using Kuroe.Flow.Control.Runtime.Engine;
using Kuroe.Flow.Records.Shared;
using Kuroe.Flow.Library;
using Kuroe.Flow.Library.Files;
using Kuroe.GraphFlow.HostInputs;
using Kuroe.FlowDescription.Json;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.GraphFlow.NodeExecution;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Persistence.Serialization.Graph;
using Kuroe.Node.Command;
using Kuroe.Node.File;
using Kuroe.Node.Llm;
using Kuroe.Process;
using Kuroe.Runner;
using Kuroe.Runner.Approval;
using Kuroe.Runner.Command;
using Kuroe.Runner.File;
using Kuroe.Runner.Input;
using Kuroe.Tool;
using Kuroe.Kernel.Tasks.Records.Shared;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Kuroe.FlowDescription.Compilation.Library;

namespace Kuroe.GraphFlow;

/// <summary>图流程装配选项：节点类型、流程库失败策略与命令节点的进程边界。</summary>
public sealed record GraphFlowOptions
{
    /// <summary>追加节点类型描述。图流程固定注册 Container、Llm、FileRead、FileWrite 与 Command。</summary>
    public Action<INodeTypeDescriptionRegistry, GraphNodeCapabilities, FlowNodeExecutionRunners>? RegisterNodeTypes { get; init; }

    /// <summary>流程库个别流程编译失败时是否继续装配。为 false 时按启动错误返回。</summary>
    public bool AllowFlowFailures { get; init; }

    /// <summary>命令节点的进程执行边界，未给出时使用系统进程。命令工具使用自己的进程边界。</summary>
    public IProcessRunner? CommandNodeProcessRunner { get; init; }

    /// <summary>Llm 节点使用的执行运行器工厂，未给出时使用核心装配的工厂。</summary>
    public IExecutionRunnerFactory? LlmRunnerFactory { get; init; }

    /// <summary>其余内置节点使用的执行运行器工厂，未给出时按组合结果建立。</summary>
    public FlowNodeExecutionRunners? ExecutionRunners { get; init; }
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
            new HostStartInputsStore(paths.HostStartInputsFile, composition.Files).Load();
        if (hostForms.IsError)
        {
            return hostForms.ErrorsOrEmptyList;
        }

        ErrorOr<HostStartInputs> hostStartInputs = HostStartInputs.Create(hostForms.Value, StartSources.Supported);
        if (hostStartInputs.IsError)
        {
            return hostStartInputs.ErrorsOrEmptyList;
        }

        ErrorOr<TaskDirectory> taskDirectory = TaskDirectory.Create(paths.TasksDirectory, composition.Files);
        if (taskDirectory.IsError)
        {
            return taskDirectory.ErrorsOrEmptyList;
        }

        ToolContractCatalog contracts = new(composition.ToolDefinitions);
        IProcessRunner process = graphOptions.CommandNodeProcessRunner ?? new ProcessRunner();
        IExecutionRunnerFactory llmRunner = graphOptions.LlmRunnerFactory ?? composition.LlmRunner;
        FlowNodeExecutionRunners runners = graphOptions.ExecutionRunners ?? new(
            llmRunner,
            new CommandExecutionRunnerFactory(process),
            new FileReadExecutionRunnerFactory(composition.Files),
            new FileWriteExecutionRunnerFactory(composition.Files),
            new InputExecutionRunnerFactory(composition.InputChannel),
            new ApprovalExecutionRunnerFactory(composition.InputChannel));
        NodeTypeDescriptionRegistry typeFiles = new();
        JsonNodeFormat nodeFormat = new();
        JsonFlowFormat flowFormat = new();
        JsonFlowImportReader importFormat = new(nodeFormat, flowFormat);
        IFlowExpander expander = new NodeLibraryExpander();
        IFlowDescriptionCompiler descriptionCompiler = new FlowDescriptionCompiler(typeFiles, expander);
        IFlowCompiler flowCompiler = new FlowCompilation(descriptionCompiler, new FlowGraphCompiler());
        ContainerNodeTypeDescriptions.RegisterInto(typeFiles);
        LlmNodeTypeDescriptions.RegisterInto(typeFiles, contracts);
        FileNodeTypeDescriptions.RegisterInto(typeFiles, paths.WorkingDirectory);
        CommandNodeTypeDescriptions.RegisterInto(typeFiles, paths.WorkingDirectory);
        GraphNodeCapabilities capabilities = new();
        FlowNodeExecution.Register(capabilities, runners);
        graphOptions.RegisterNodeTypes?.Invoke(typeFiles, capabilities, runners);

        ErrorOr<FlowService> flows = FlowService.Create(
            new FlowStore(
                paths.NodesFile,
                paths.FlowsFile,
                paths.WorkingDirectory,
                nodeFormat,
                nodeFormat,
                flowFormat,
                flowFormat,
                importFormat,
                composition.Files),
            flowCompiler,
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
        services.AddSingleton(descriptionCompiler);
        services.AddSingleton<IFlowGraphCompiler>(new FlowGraphCompiler());
        services.AddSingleton<IFlowRecordCodec, GraphFlowRecordCodec>();
        services.AddSingleton<IExecutionNodeDeclarationResolver>(sp => new ExecutionNodeDeclarationResolver(
            new NodeModelResolver(sp.GetRequiredService<IModelAccess>())));
        GraphFlowTemplateSource templates = new(flows.Value, hostStartInputs.Value);
        composition.FlowTemplates.Register(templates);
        services.AddSingleton<IFlowTemplateSource>(templates);
        services.AddSingleton(sp => new FlowProvider(
            new GraphFlowIdentity(flows.Value),
            new GraphFlowSubmission(
                flows.Value,
                flows.Value,
                hostStartInputs.Value,
                sp.GetRequiredService<IHostTaskStart>(),
                sp.GetRequiredService<IExecutionNodeDeclarationResolver>(),
                sp.GetRequiredService<ITaskExecutionGateway>(),
                capabilities,
                composition.PersistTasks ? flows.Value : null),
            new GraphFlowStateBinder(),
            new GraphFlowControlFactory(
                sp.GetRequiredService<IExecutionNodeDeclarationResolver>(),
                sp.GetRequiredService<ITaskExecutionGateway>()),
            new GraphFlowPresentation(),
            new GraphFlowRecordSource()));

        return Result.Success;
    }
}
