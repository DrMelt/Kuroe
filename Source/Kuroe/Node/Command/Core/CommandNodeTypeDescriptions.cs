using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Process;

namespace Kuroe.Node.Command;

/// <summary>命令节点描述的注册入口。</summary>
public static class CommandNodeTypeDescriptions
{
    /// <summary>把命令节点的描述注册进注册表。工作目录是执行目录解析与边界校验的基准，进程执行边界由装配处给出。</summary>
    public static void RegisterInto(
        INodeTypeDescriptionRegistry types,
        string workingDirectory,
        IProcessRunner? process = null) =>
        types.Register(new CommandNodeTypeDescription(workingDirectory, process ?? new ProcessRunner()));
}