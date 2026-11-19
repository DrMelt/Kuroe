using Kuroe.FlowDescription.Shared.Compilation.NodeTypes;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Node.File;

/// <summary>文件节点描述的注册入口。</summary>
public static class FileNodeTypeDescriptions
{
    /// <summary>把文件读取与文件写入节点的描述注册进注册表。工作区根是路径解析的边界，由装配处给出。</summary>
    public static void RegisterInto(
        INodeTypeDescriptionRegistry registry,
        string rootDirectory,
        IExecutionRunnerFactory readRunnerFactory,
        IExecutionRunnerFactory writeRunnerFactory)
    {
        registry.Register(new FileReadNodeTypeDescription(rootDirectory, readRunnerFactory));
        registry.Register(new FileWriteNodeTypeDescription(rootDirectory, writeRunnerFactory));
    }
}