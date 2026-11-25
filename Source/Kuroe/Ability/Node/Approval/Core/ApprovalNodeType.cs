using Kuroe.FlowDescription.Description.Shared.Reporting;
using Kuroe.FlowDescription.Definition.Shared.Validation;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;

namespace Kuroe.Ability.Node.Approval;

/// <summary>审批节点类型：启动执行后等待批准，批准即按同名输出端口放出停驻取值。取消后不再接收外部输入。</summary>
public sealed class ApprovalNodeType(ApprovalParameters? parameters)
    : INodeType, INodeTypeValidation
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
}
