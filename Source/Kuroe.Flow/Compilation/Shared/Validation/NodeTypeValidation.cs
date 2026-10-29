using ErrorOr;
using Kuroe.Flow.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>节点类型校验的分派：类型实现校验契约时调用，否则给出配置错误。</summary>
public static class NodeTypeValidation
{
    /// <summary>校验一个节点，类型未实现校验契约时经 fail 报出。</summary>
    public static void Validate(this INodeType type, NodeSpec node, NodeScope scope, Action<Error> fail)
    {
        if (type is INodeTypeValidation validation)
        {
            validation.Validate(node, scope, fail);
            return;
        }

        fail(scope.Node(node.Label.Value, $"节点类型 {type.Kind.Value} 没有实现校验契约。"));
    }
}