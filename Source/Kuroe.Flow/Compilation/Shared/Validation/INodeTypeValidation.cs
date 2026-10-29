using ErrorOr;
using Kuroe.Flow.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>节点类型的校验契约：形状落地后仍要成立的不变量，逐条经 fail 报出。</summary>
public interface INodeTypeValidation
{
    /// <summary>校验一个节点，scope 给出流程或节点库的作用域。</summary>
    void Validate(NodeSpec node, NodeScope scope, Action<Error> fail);
}