using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Shared;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.File;

/// <summary>文件节点在任务内的运行期对象：保存文件声明，读写行为由具体节点实现。</summary>
internal abstract class FileRuntime(ExecutableNode executable, FileParameters parameters)
    : SelfDrivenRuntime(executable)
{
    /// <summary>本节点的类型专属声明。</summary>
    protected FileParameters Parameters { get; } = parameters;

    /// <summary>提交时锁定的执行节点定义。</summary>
    protected ExecutableNode Executable => (ExecutableNode)Node;

    /// <inheritdoc/>
    protected override string Target => Parameters.Path;

    /// <summary>按 BasePath 输入解析文件路径，成功时给出工作区内的绝对路径。</summary>
    protected string? ResolveTarget(NodeInputValues inputs, out string fullPath)
    {
        fullPath = string.Empty;

        string? basePath = inputs.Text(FileNodePorts.BasePath);
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return "BasePath 未连接或为空。";
        }

        if (!WorkspacePath.TryResolve(Parameters.RootDirectory, basePath, out string baseDirectory))
        {
            return $"BasePath {basePath} 不是工作区内的合法路径。";
        }

        if (!WorkspacePath.TryResolveWithin(Parameters.RootDirectory, baseDirectory, Parameters.Path, out fullPath))
        {
            return $"Path {Parameters.Path} 不是工作区内的合法路径。";
        }

        return null;
    }
}