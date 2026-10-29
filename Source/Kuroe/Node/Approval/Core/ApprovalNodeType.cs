using ErrorOr;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.NodeTypes;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Approval;

/// <summary>审批节点类型：不启动执行，输入齐备即停在待批准，批准后按同名输出端口放出停驻时的取值。取消后不再接收外部输入。
/// 外部输入协议是建运行期对象所需的运行期依赖，随类型实例带到运行期对象，节点的执行经它声明接收与接受交付。</summary>
public sealed class ApprovalNodeType(ApprovalParameters? parameters, IInputChannel channel) : INodeType, INodeTypeValidation, INodeRuntimeFactory, IQuestionNodeType
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public ApprovalParameters? Parameters { get; } = parameters;

    /// <summary>等待外部输入时给用户的提问。</summary>
    public string? Question => Parameters?.Question;

    /// <summary>类型名。</summary>
    public NodeKind Kind => ApprovalNodeTypes.Name;

    /// <summary>审批节点类型的静态形状。</summary>
    public NodeTypeShape Shape => ApprovalNodeTypes.Shape;

    /// <summary>审批节点的类型规则：对外端口取自身输入端口，不接受执行方式与校验。</summary>
    public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
    {
        ExecutableSpec spec = node.Spec!;

        void Reject(bool condition, string message)
        {
            if (condition)
            {
                fail(scope.Node(node.Label.Value, message));
            }
        }

        Reject(node.Inputs.Count == 0, "审批节点必须声明输入端口，对外端口取自身输入端口。");
        Reject(node.Inputs.Count(port => port.Type.Semantics == PortSemantics.Context) > 1, "审批节点至多声明一个 Context 输入端口。");
        Reject(spec.Validate is not null, "审批节点不启动执行，不能声明 Validate。");
        Reject(node.MaxExecutions is not null, "审批节点不启动执行，不能声明 MaxExecutions。");
    }

    /// <summary>审批节点的运行期对象。</summary>
    public NodeRuntime Create(GraphNode node, INodeContext context) =>
        new ApprovalRuntime((ExecutableNode)node, channel, context);
}
