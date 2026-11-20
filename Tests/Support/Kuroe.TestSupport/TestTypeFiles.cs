using Kuroe.GraphFlow.NodeExecution;
using Kuroe.FlowDescription.Compilation.NodeTypes;
using Kuroe.FlowDescription.Compilation.Shared.NodeTypes;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Node.Approval;
using Kuroe.Node.Input;
using Kuroe.Node.Llm;
using Kuroe.Tool;
using Kuroe.Tool.Implementation.Dotnet;
using Kuroe.Tool.Implementation.File;
using Kuroe.Tool.Implementation.Git;
using Kuroe.Tool.Implementation.Time;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.TestSupport;

/// <summary>测试用的节点类型描述注册表：引擎内置的容器与扩展提供的各节点类型。</summary>
public static class TestTypeFiles
{
    /// <summary>核心可变变量工具的契约，供直接编译流程的测试校验 ToolPorts。</summary>
    public static IToolContractCatalog Contracts { get; } = new ToolContractCatalog(
    [
        .. new FileToolSource(new InMemoryWorkspaceFiles()).Definitions,
        .. new GitToolSource().Definitions,
        .. new DotnetToolSource().Definitions,
        .. new TimeToolSource(TimeProvider.System).Definitions,
    ]);

    /// <summary>测试节点类型使用的执行运行器工厂，不建立运行器。</summary>
    public static IExecutionRunnerFactory Runners { get; } = new UnusedRunnerFactory();

    /// <summary>建出一份含全部可用类型的节点类型描述注册表。</summary>
    public static NodeTypeDescriptionRegistry Registry()
    {
        NodeTypeDescriptionRegistry files = new();
        ContainerNodeTypeDescriptions.RegisterInto(files);
        LlmNodeTypeDescriptions.RegisterInto(files, Contracts);
        RegisterAll(files);

        return files;
    }

    /// <summary>把输入与审批节点描述注册进注册表。</summary>
    public static void RegisterAll(INodeTypeDescriptionRegistry files)
    {
        InputNodeTypeDescriptions.RegisterInto(files);
        ApprovalNodeTypeDescriptions.RegisterInto(files);
    }

    /// <summary>建出含测试节点能力的图能力映射。</summary>
    public static GraphNodeCapabilities GraphCapabilities(IExecutionRunnerFactory? runner = null)
    {
        IExecutionRunnerFactory runners = runner ?? Runners;
        GraphNodeCapabilities capabilities = new();
        capabilities.Register(LlmNodeTypes.Name, type => LlmNodeExecution.Bind(type, runners));
        FlowNodeExecution.RegisterInteractive(capabilities, runners, runners);

        return capabilities;
    }

    private sealed class UnusedRunnerFactory : IExecutionRunnerFactory
    {
        public IExecutionRunner Create(
            IExecutionParameters parameters,
            IExecutionRequestEntry requests) =>
            throw new InvalidOperationException("测试节点类型不建立运行器。");
    }
}
