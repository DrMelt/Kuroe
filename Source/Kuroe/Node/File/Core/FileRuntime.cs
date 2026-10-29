using ErrorOr;
using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Graph;
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

    /// <summary>按路径变量解析文件路径，成功时给出工作区内的绝对路径。</summary>
    protected string? ResolveTarget(NodeInputValues inputs, out string fullPath)
    {
        fullPath = string.Empty;

        ErrorOr<PathResolution> resolved = PathVariables.Resolve(
            Parameters.Variables,
            port => PortText(inputs, new PortName(port)),
            Parameters.RootDirectory);
        if (resolved.IsError)
        {
            return resolved.FirstError.Description;
        }

        ErrorOr<string> path = PathVariables.Expand(
            Parameters.Path,
            resolved.Value.Variables,
            resolved.Value.BaseDirectory,
            Parameters.RootDirectory,
            "Path");
        if (path.IsError)
        {
            return path.FirstError.Description;
        }

        fullPath = path.Value;

        return null;
    }

    /// <summary>按输入端口取文本：静态端口给静态值，动态端口给输入值。</summary>
    private static string? PortText(NodeInputValues inputs, PortName port) => inputs.Text(port);
}
