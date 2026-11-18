using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration.Files;
using Kuroe.Control.Shared;
using Kuroe.Flow.Compilation;
using Kuroe.Flow.Compilation.Description;
using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Compilation.Shared.Flow;
using Kuroe.Flow.Control.Runtime.Catalog;
using Kuroe.Flow.Control.Runtime.Engine;
using Kuroe.Flow.Records.Shared;
using BuiltinNodeCatalog = Kuroe.Flow.Hosting.Workflows.BuiltinNodes.BuiltinNodes;
using Kuroe.Flow.Hosting.Workflows.Flows;
using Kuroe.Flow.Hosting.Workflows.Flows.Files;
using Kuroe.Flow.Hosting.Workflows.Tasks;
using Kuroe.Flow.Json;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Persistence.Graph;
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
using Kuroe.Tasks.Records.Shared;
using Kuroe.Execution.Shared.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Flow.Hosting;

/// <summary>图流程装配选项：节点类型、流程库失败策略与命令节点的进程边界。</summary>
public sealed record GraphFlowOptions
{
    /// <summary>追加节点类型描述。图流程固定注册 Container、Llm、FileRead、FileWrite 与 Command。</summary>
    public Action<INodeTypeDescriptionRegistry, GraphExecutionRunners>? RegisterNodeTypes { get; init; }

    /// <summary>流程库个别流程编译失败时是否继续装配。为 false 时按启动错误返回。</summary>
    public bool AllowFlowFailures { get; init; }

    /// <summary>命令节点的进程执行边界，未给出时使用系统进程。命令工具使用自己的进程边界。</summary>
    public IProcessRunner? CommandNodeProcessRunner { get; init; }

    /// <summary>Llm 节点使用的执行运行器工厂，未给出时使用核心装配的工厂。</summary>
    public IExecutionRunnerFactory? LlmRunnerFactory { get; init; }

    /// <summary>其余内置节点使用的执行运行器工厂，未给出时按组合结果建立。</summary>
    public GraphExecutionRunners? ExecutionRunners { get; init; }
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
        IProcessRunner process = graphOptions.CommandNodeProcessRunner ?? new ProcessRunner();
        IExecutionRunnerFactory llmRunner = graphOptions.LlmRunnerFactory ?? composition.LlmRunner;
        GraphExecutionRunners runners = graphOptions.ExecutionRunners ?? new(
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
        ContainerNodeTypeDescriptions.RegisterInto(typeFiles);
        LlmNodeTypeDescriptions.RegisterInto(typeFiles, llmRunner, contracts);
        FileNodeTypeDescriptions.RegisterInto(
            typeFiles,
            paths.WorkingDirectory,
            runners.FileRead,
            runners.FileWrite);
        CommandNodeTypeDescriptions.RegisterInto(typeFiles, paths.WorkingDirectory, runners.Command);
        graphOptions.RegisterNodeTypes?.Invoke(typeFiles, runners);

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
            BuiltinNodeCatalog.Create(llmRunner),
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