using Kuroe.Execution.Shared;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Runner;
using Kuroe.Tasks.Shared;

namespace Kuroe.Node.Approval;

/// <summary>审批节点类型：启动执行后等待批准，批准即按同名输出端口放出停驻取值。取消后不再接收外部输入。</summary>
public sealed class ApprovalNodeType(ApprovalParameters? parameters)
    : INodeType, INodeTypeValidation, INodeOutputProjection, IExecutionParameterProvider
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public ApprovalParameters? Parameters { get; } = parameters;

    /// <summary>类型名。</summary>
    public NodeKind Kind => ApprovalNodeTypes.Name;

    /// <summary>审批节点类型的静态形状。</summary>
    public NodeTypeShape Shape => ApprovalNodeTypes.Shape;

    /// <summary>审批节点的类型规则：对外端口取自身输入端口，不接受执行上限。</summary>
    public void Validate(NodeSpec node, NodeScope scope, ValidationErrorHandler fail)
    {
        void Reject(bool condition, string message)
        {
            if (condition)
            {
                fail(scope.Node(node.Label.Value, message));
            }
        }

        Reject(node.Inputs.Count == 0, "审批节点必须声明输入端口，对外端口取自身输入端口。");
        Reject(node.Inputs.Count(port => port.Type.Semantics == PortSemantics.Context) > 1, "审批节点至多声明一个 Context 输入端口。");
        Reject(node.MaxExecutions is not null, "审批节点不能声明 MaxExecutions。");
    }

    /// <summary>按本次执行建立审批参数，等待步骤取执行目标。</summary>
    public IExecutionParameters Create(ExecutionParameterContext context) =>
        new ApprovalExecutionParameters(Parameters?.Question, ExecutionTargetPaths.StepPath(context.Target));

    /// <summary>把停驻输入按同名输出端口投影为节点发布，保留出处与多段内容，不携带触发。</summary>
    public Publication Project(ExecutionId execution, GraphExecutionInput input, ExecutionOutput output)
    {
        Dictionary<PortName, List<ContextValue>> byPort = [];
        foreach (InputValue value in input.Inputs)
        {
            if (!byPort.TryGetValue(value.Port, out List<ContextValue>? values))
            {
                values = [];
                byPort[value.Port] = values;
            }

            values.Add(value.Value);
        }

        Dictionary<PortName, IReadOnlyList<ContextValue>> content = input.OutputPorts
            .Where(port => byPort.ContainsKey(port.Name))
            .ToDictionary(port => port.Name, port => (IReadOnlyList<ContextValue>)byPort[port.Name]);

        return new Publication(0, new Dictionary<PortName, bool>(), content);
    }
}