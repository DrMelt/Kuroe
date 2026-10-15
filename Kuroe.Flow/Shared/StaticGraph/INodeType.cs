using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph;

namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>一个节点类型：静态形状、类型规则与运行期对象的建立。由实现方注册进 <see cref="NodeTypeRegistry"/>。
/// 形状之外的声明约束由 <see cref="Validate"/> 表达，运行期行为由 <see cref="Create"/> 给出。</summary>
public interface INodeType
{
    /// <summary>类型名，与文件里 Kind 的取值对应。</summary>
    NodeKind Kind { get; }

    /// <summary>静态形状。</summary>
    NodeTypeShape Shape { get; }

    /// <summary>类型参数的文件形状，不声明参数的类型为空。</summary>
    INodeParameterJson? Parameters => null;

    /// <summary>类型规则：形状落地后仍要成立的不变量，逐条经 fail 报出。与类型无关的规则不在其中。</summary>
    void Validate(NodeSpec node, NodeScope scope, Action<Error> fail);

    /// <summary>建立运行期对象。不派发 run 的类型也在其中表达停驻与产出。</summary>
    NodeRuntime Create(GraphNode node, INodeContext context);
}
