using Kuroe.FlowDescription.Compilation.Shared.NodeTypes;

namespace Kuroe.Ability.Node.Command;

/// <summary>命令节点描述的注册入口。</summary>
public static class CommandNodeTypeDescriptions
{
    /// <summary>把命令节点的描述注册进注册表。工作区根是路径解析的边界。</summary>
    public static void RegisterInto(
        INodeTypeDescriptionRegistry types,
        string rootDirectory) =>
        types.Register(new CommandNodeTypeDescription(rootDirectory));
}
