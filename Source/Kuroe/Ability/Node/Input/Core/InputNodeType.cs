using Kuroe.FlowDescription.Description.Shared.Reporting;
using Kuroe.FlowDescription.Definition.Shared.Validation;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;

namespace Kuroe.Ability.Node.Input;

/// <summary>输入节点类型：启动执行后等待用户回答，回答即产出放行下游。</summary>
public sealed class InputNodeType(InputParameters? parameters)
    : INodeType, INodeTypeValidation
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public InputParameters? Parameters { get; } = parameters;

    /// <summary>类型名。</summary>
    public NodeKind Kind => InputNodeTypes.Name;

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
}
