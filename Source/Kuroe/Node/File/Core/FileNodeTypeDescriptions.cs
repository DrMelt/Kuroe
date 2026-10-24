using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe.Node.File;

/// <summary>文件节点描述的注册入口。</summary>
public static class FileNodeTypeDescriptions
{
    /// <summary>把文件读取与文件写入节点的描述注册进注册表。工作目录是路径参数解析与边界校验的基准，由装配处给出。</summary>
    public static void RegisterInto(INodeTypeDescriptionRegistry files, string workingDirectory)
    {
        files.Register(new FileReadNodeTypeDescription(workingDirectory));
        files.Register(new FileWriteNodeTypeDescription(workingDirectory));
    }
}