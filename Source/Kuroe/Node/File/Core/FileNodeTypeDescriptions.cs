using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.Shared.StaticGraph.Nodes;

namespace Kuroe.Node.File;

/// <summary>文件节点描述的注册入口。</summary>
public static class FileNodeTypeDescriptions
{
    /// <summary>把文件读取与文件写入节点的描述注册进注册表。工作区根是路径解析的边界，由装配处给出。</summary>
    public static void RegisterInto(INodeTypeDescriptionRegistry files, string rootDirectory)
    {
        files.Register(new FileReadNodeTypeDescription(rootDirectory));
        files.Register(new FileWriteNodeTypeDescription(rootDirectory));
    }
}