using Kuroe.Execution.Shared;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Input.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Node.Input;

/// <summary>输入节点类型：启动执行后等待用户回答，回答即产出放行下游。
/// 外部输入协议是执行运行器的运行期依赖，随类型实例带到运行期对象。</summary>
public sealed class InputNodeType(InputParameters? parameters, IInputChannel channel)
    : INodeType, INodeTypeValidation, IExecutionRunnerFactory, INodeOutputProjection
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public InputParameters? Parameters { get; } = parameters;

    /// <summary>类型名。</summary>
    public NodeKind Kind => InputNodeTypes.Name;

    /// <summary>输入节点类型的静态形状。</summary>
    public NodeTypeShape Shape => InputNodeTypes.Shape;

    /// <summary>执行运行器的种类。</summary>
    string IExecutionRunnerFactory.Kind => InputNodeTypes.Name.Value;

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

    /// <summary>输入节点的执行运行器。</summary>
    public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) =>
        new InputExecutionRunner(channel, Parameters?.Question);

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
