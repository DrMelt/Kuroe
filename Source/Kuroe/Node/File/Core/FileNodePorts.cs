using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Node.File;

/// <summary>文件节点共用的输入端口名。</summary>
internal static class FileNodePorts
{
    /// <summary>路径解析的基准目录。</summary>
    internal static PortName BasePath { get; } = new("BasePath");
}