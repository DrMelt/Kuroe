using Kuroe.Flow.Compilation.Shared.NodeTypes;

namespace Kuroe.Flow.NodeType.Container;

/// <summary>引擎内置容器描述的注册入口。</summary>
public static class ContainerNodeTypeDescriptions
{
    /// <summary>把容器的描述注册进注册表。</summary>
    public static void RegisterInto(INodeTypeDescriptionRegistry files) => files.Register(new ContainerNodeTypeDescription());
}