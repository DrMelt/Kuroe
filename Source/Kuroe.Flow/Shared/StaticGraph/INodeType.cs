using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph;

namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>一个节点类型：类型名、静态形状、类型规则与运行期行为，由实现方建出实例挂在节点上。
/// 引擎按形状解析声明与推进执行，类型专属声明封在实例里，引擎只透传不解释。
/// 派发执行的类型与组织成员的容器，其运行期对象由引擎按形状建立。</summary>
public interface INodeType
{
    /// <summary>类型名，与文件里 Kind 的取值对应。</summary>
    NodeKind Kind { get; }

    /// <summary>静态形状。</summary>
    NodeTypeShape Shape { get; }

    /// <summary>类型规则：形状落地后仍要成立的不变量，逐条经 fail 报出。与类型无关的规则不在其中。</summary>
    void Validate(NodeSpec node, NodeScope scope, Action<Error> fail);

    /// <summary>建立该节点的运行期对象：停驻与产出的表达。</summary>
    NodeRuntime Create(GraphNode node, INodeContext context) =>
        throw new InvalidOperationException($"{Kind.Value} 类型的运行期对象由引擎按形状建立。");
}
