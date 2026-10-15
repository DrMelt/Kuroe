using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flow.Node.Approval;

/// <summary>审批节点类型：不启动 run，启动条件齐备即停在待批准，批准后按输入端口转发上游产出。</summary>
internal sealed class ApprovalNodeType : INodeType
{
    /// <summary>类型名。</summary>
    public NodeTypeName Type => ApprovalNodeTypes.Name;

    /// <summary>审批节点类型的形状：对外端口由输入端口派生，Signal 只作驱动。</summary>
    public NodeTypeShape Shape { get; } = new()
    {
        DerivesOutputsFromInputs = true,
        InputTypes = [PortType.Text, PortType.Context, PortType.Signal],
        OutputTypes = [PortType.Text, PortType.Context, PortType.Signal],
    };

    /// <summary>审批节点的类型规则：对外端口取自身输入端口，不接受执行配置与模型。</summary>
    public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
    {
        ExecutableSpec execution = node.Execution!;

        void Reject(bool condition, string message)
        {
            if (condition)
            {
                fail(scope.Node(node.Name.Value, message));
            }
        }

        Reject(node.Inputs.Count == 0, "审批节点必须声明输入端口，对外端口取自身输入端口。");
        Reject(node.Inputs.Count(port => port.Type == PortType.Context) > 1, "审批节点至多声明一个 Context 输入端口。");
        Reject(node.Model is not null, "审批节点不启动 run，不能声明模型选择。");
        Reject(execution.Tools.Count > 0, "审批节点不启动 run，不能声明 Tools。");
        Reject(execution.Prompt is { Length: > 0 }, "审批节点不启动 run，不能声明 Prompt。");
        Reject(execution.Mode != NodeMode.Single, "审批节点只能整节点等待批准。");
        Reject(execution.Branch is not null, "审批节点不启动 run，不能声明 Branch。");
        Reject(execution.Validate is not null, "审批节点不启动 run，不能声明 Validate。");
        Reject(node.SystemPrompt.Count > 0, "审批节点不启动 run，不能声明系统指令。");
        Reject(node.MaxRuns is not null, "审批节点不启动 run，不能声明 MaxRuns。");
    }

    /// <summary>审批节点的运行期对象。</summary>
    public NodeRuntime Create(GraphNode node, INodeContext context) =>
        new ApprovalRuntime((ExecutableNode)node, context);
}
