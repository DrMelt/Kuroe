using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.NodeType.Llm;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.NodeType;

/// <summary>引擎内置的节点类型：由模型执行的节点与容器。装配前并入注册表，已有同名注册时保留调用方注册的实现。</summary>
internal static class BuiltinTypeRegistry
{
    /// <summary>把内置类型并入注册表，同名已注册时不覆盖。</summary>
    internal static void RegisterInto(NodeTypeRegistry registry)
    {
        foreach (INodeType type in All().Where(type => registry.Of(type.Kind) is null))
        {
            registry.Register(type);
        }
    }

    private static IReadOnlyList<INodeType> All() => [new LlmNodeType(), new ContainerNodeType()];
}
