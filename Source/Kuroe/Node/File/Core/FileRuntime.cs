using Kuroe.Flow.Runtime.Nodes;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.File;

/// <summary>文件节点在任务内的运行期对象：保存文件声明，读写行为由具体节点实现。</summary>
internal abstract class FileRuntime(ExecutableNode executable, FileParameters parameters)
    : SelfDrivenRuntime(executable)
{
    /// <summary>本节点的类型专属声明。</summary>
    protected FileParameters Parameters { get; } = parameters;

    /// <inheritdoc/>
    protected override string Target => Parameters.Path;
}