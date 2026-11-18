using Kuroe.Flow.Node.Shared.StaticGraph.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Runner.Input;
using Kuroe.Tasks.Shared;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Node.Input;

/// <summary>输入节点类型：启动执行后等待用户回答，回答即产出放行下游。</summary>
public sealed class InputNodeType(
    InputParameters? parameters,
    IExecutionRunnerFactory runnerFactory)
    : INodeType, INodeTypeValidation, INodeOutputProjection, IExecutionParameterProvider, IExecutionRunnerFactoryProvider
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public InputParameters? Parameters { get; } = parameters;

    /// <summary>类型名。</summary>
    public NodeKind Kind => InputNodeTypes.Name;

    /// <summary>本节点使用的执行运行器工厂。</summary>
    public IExecutionRunnerFactory RunnerFactory { get; } = runnerFactory;

    /// <summary>输入节点类型的静态形状。</summary>
    public NodeTypeShape Shape => InputNodeTypes.Shape;

    /// <summary>输入节点的类型规则：只以用户输入为产出。</summary>
    public void Validate(NodeSpec node, NodeScope scope, ValidationErrorHandler fail)
    {
        void Reject(bool condition, string message)
        {
            if (condition)
            {
                fail(scope.Node(node.Label.Value, message));
            }
        }

        Reject(node.Outputs.Count > 1, "输入节点最多声明一个输出端口，回答写入该端口。");
        Reject(node.Outputs.Any(port => port.Trigger), "输入节点的输出端口不能使用触发。");
    }

    /// <summary>按本次执行建立输入参数，等待步骤取执行步骤标识。</summary>
    public IExecutionParameters Create(ExecutionParameterContext context) =>
        new InputExecutionParameters(Parameters?.Question, ExecutionStepPaths.ToFlowStepPath(context.Step));

    /// <summary>把回答文本投影为承载文本的输出端口，出处指向本节点的输入。</summary>
    public Publication Project(ExecutionId execution, GraphExecutionInput input, ExecutionOutput output)
    {
        Dictionary<PortName, IReadOnlyList<ContextValue>> content = output.Content.Length > 0
            ? input.OutputPorts
                .Where(port => PortRules.CarriesText(port.Type))
                .ToDictionary(
                    port => port.Name,
                    port => (IReadOnlyList<ContextValue>)
                        [new ContextValue(new InputSource(input.Node, port.Name), output.Content)])
            : [];

        return new Publication(0, new Dictionary<PortName, bool>(), content);
    }
}