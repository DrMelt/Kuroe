using Kuroe.FlowDescription.Shared.StaticGraph.Validation;
using Kuroe.FlowDescription.Shared.Model;
using Kuroe.FlowDescription.Shared.StaticGraph.Nodes;
using Kuroe.FlowDescription.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Flow.TestSupport;

/// <summary>测试用的执行节点类型：派发执行，端口取值域与执行节点一致，不接受任何类型专属声明。</summary>
public sealed class TestExecutableNodeType(
    bool withContractInput = true,
    IExecutionRunnerFactory? runnerFactory = null)
    : INodeType, INodeTypeValidation, IExecutionRunnerFactoryProvider
{
    /// <summary>测试执行节点默认使用的执行运行器工厂，不用于真正建立运行器。</summary>
    public static IExecutionRunnerFactory Factory { get; } = new UnusedFactory();

    /// <summary>类型名。</summary>
    public NodeKind Kind => TestNodeTypes.ExecutableKind;

    /// <summary>本节点使用的执行运行器工厂。</summary>
    public IExecutionRunnerFactory RunnerFactory { get; } = runnerFactory ?? Factory;

    /// <summary>测试执行节点使用的静态输入端口名。</summary>
    public static PortName StaticPort { get; } = new("静态值");

    /// <summary>测试执行节点类型不预置输入端口，端口由节点声明给出。</summary>
    public NodeTypeShape Shape { get; } = new()
    {
        DispatchesExecutions = true,
        AcceptsInferredInputs = true,
        ContractInputs = withContractInput
            ? [new InputPort(StaticPort, PortTypes.Text, "测试静态输入。", "执行者", mode: PortMode.Constant)]
            : [],
        InputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
        OutputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
    };

    /// <summary>替身的类型规则：执行上限必须是正整数，覆盖类型规则经节点作用域报出的路径。</summary>
    public void Validate(NodeSpec node, NodeScope scope, ValidationErrorHandler fail)
    {
        if (node.Spec?.MaxExecutions is { } limit && limit < 1)
        {
            fail(scope.Node(node.Label.Value, "MaxExecutions 必须是正整数。"));
        }
    }

    private sealed class UnusedFactory : IExecutionRunnerFactory
    {
        public IExecutionRunner Create(
            IExecutionParameters parameters,
            IExecutionRequestEntry requests) =>
            throw new InvalidOperationException("测试节点类型不建立运行器。");
    }
}
