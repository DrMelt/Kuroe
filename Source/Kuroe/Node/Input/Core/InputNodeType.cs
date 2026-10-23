using ErrorOr;
using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Input;

/// <summary>输入节点类型：不启动执行，依赖齐备后停在待输入，用户回答即产出放行下游。
/// 外部输入协议是建运行期对象所需的运行期依赖，随类型实例带到运行期对象，节点的执行经它声明接收与接受交付。</summary>
public sealed class InputNodeType(InputParameters? parameters, IInputChannel channel) : INodeType, IQuestionNodeType
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public InputParameters? Parameters { get; } = parameters;

    /// <summary>等待外部输入时给用户的提问。</summary>
    public string? Question => Parameters?.Question;

    /// <summary>类型名。</summary>
    public NodeKind Kind => InputNodeTypes.Name;

    /// <summary>输入节点类型的静态形状。</summary>
    public NodeTypeShape Shape => InputNodeTypes.Shape;

    /// <summary>输入节点的类型规则：只以用户输入为产出，不接受执行方式与校验。</summary>
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

        Reject(node.Model is not null, "输入节点不启动执行，不能声明模型选择。");
        Reject(spec.Validate is not null, "输入节点不启动执行，不能声明 Validate。");
        Reject(node.Outputs.Count > 1, "输入节点最多声明一个输出端口，回答写入该端口。");
    }

    /// <summary>输入节点的运行期对象。</summary>
    public NodeRuntime Create(GraphNode node, INodeContext context) =>
        new InputRuntime((ExecutableNode)node, channel, context);
}
