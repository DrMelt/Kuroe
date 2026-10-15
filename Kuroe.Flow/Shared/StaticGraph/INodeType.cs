using ErrorOr;

namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>一个节点类型：静态形状与类型规则。由实现方注册进 <see cref="NodeTypeRegistry"/>。
/// 形状之外的声明约束由 <see cref="Validate"/> 表达；参数段的文件形状由文件层的 INodeParameterFile 给出；
/// 不派发 run 的类型自建运行期对象的职责在 <see cref="INodeRuntimeFactory"/>。</summary>
public interface INodeType
{
    /// <summary>类型名，与文件里 Kind 的取值对应。</summary>
    NodeKind Kind { get; }

    /// <summary>静态形状。</summary>
    NodeTypeShape Shape { get; }

    /// <summary>类型规则：形状落地后仍要成立的不变量，逐条经 fail 报出。与类型无关的规则不在其中。</summary>
    void Validate(NodeSpec node, NodeScope scope, Action<Error> fail);
}
