using Kuroe.FlowDescription.Compilation.Shared.NodeTypes;

namespace Kuroe.Node.File;

/// <summary>文件节点描述的注册入口。</summary>
public static class FileNodeTypeDescriptions
{
    /// <summary>把文件读取与文件写入节点的描述注册进注册表。工作区根是路径解析的边界。</summary>
    public static void RegisterInto(
        INodeTypeDescriptionRegistry registry,
        string rootDirectory)
    {
        registry.Register(new FileReadNodeTypeDescription(rootDirectory));
        registry.Register(new FileWriteNodeTypeDescription(rootDirectory));
    }
}
