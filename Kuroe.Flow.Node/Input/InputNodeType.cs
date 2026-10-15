using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flow.Node.Input;

/// <summary>输入节点类型：不启动 run，依赖齐备后停在待输入，用户回答即产出放行下游。</summary>
internal sealed class InputNodeType : INodeType
{
    /// <summary>类型名。</summary>
    public NodeTypeName Type => InputNodeTypes.Name;

    /// <summary>输入节点类型的形状：不派发 run，输入侧只作驱动与内容引用，输出侧只写回答。</summary>
    public NodeTypeShape Shape { get; } = new()
    {
        StartsUnaided = true,
        InputTypes = [PortType.Text, PortType.Signal],
        OutputTypes = [PortType.Text],
    };

    /// <summary>输入节点的类型规则：只以用户输入为产出，不接受执行配置。</summary>
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

        Reject(node.Model is not null, "输入节点不启动 run，不能声明模型选择。");
        Reject(execution.Tools.Count > 0, "输入节点不启动 run，不能声明 Tools。");
        Reject(execution.Prompt is { Length: > 0 }, "输入节点不启动 run，不能声明 Prompt。");
        Reject(execution.Validate is not null, "输入节点不启动 run，不能声明 Validate。");
        Reject(execution.Mode != NodeMode.Single, "输入节点只能整节点等待用户输入。");
        Reject(execution.Branch is not null, "输入节点不启动 run，不能声明 Branch。");
        Reject(node.Outputs.Count > 1, "输入节点最多声明一个输出端口，回答写入该端口。");
        Reject(node.SystemPrompt.Count > 0, "输入节点不启动 run，不能声明系统指令。");
    }

    /// <summary>输入节点的运行期对象。</summary>
    public NodeRuntime Create(GraphNode node, INodeContext context) => new InputRuntime((ExecutableNode)node);
}
