using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flow.Node.Input;

/// <summary>输入节点类型：不启动 run，依赖齐备后停在待输入，用户回答即产出放行下游。</summary>
public sealed class InputNodeType(InputParameters? parameters) : INodeType
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public InputParameters? Parameters { get; } = parameters;

    /// <summary>类型名。</summary>
    public NodeKind Kind => InputNodeTypes.Name;

    /// <summary>输入节点类型的静态形状。</summary>
    public NodeTypeShape Shape => InputNodeTypes.Shape;

    /// <summary>输入节点的类型规则：只以用户输入为产出，不接受执行方式与校验。</summary>
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
        Reject(execution.Validate is not null, "输入节点不启动 run，不能声明 Validate。");
        Reject(execution.Mode != NodeMode.Single, "输入节点只能整节点等待用户输入。");
        Reject(execution.Branch is not null, "输入节点不启动 run，不能声明 Branch。");
        Reject(node.Outputs.Count > 1, "输入节点最多声明一个输出端口，回答写入该端口。");
    }

    /// <summary>输入节点的运行期对象。</summary>
    public NodeRuntime Create(GraphNode node, INodeContext context) => new InputRuntime((ExecutableNode)node);
}
