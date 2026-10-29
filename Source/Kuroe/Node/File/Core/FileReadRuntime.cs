using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;
using IOFile = System.IO.File;

namespace Kuroe.Node.File;

/// <summary>文件读取节点的运行期对象：消费引擎装入的输入，按路径变量解析路径并读取文件，内容写入唯一的输出端口。</summary>
internal sealed class FileReadRuntime(ExecutableNode executable, FileParameters parameters)
    : FileRuntime(executable, parameters)
{
    /// <inheritdoc/>
    protected override string Action => "读取";

    /// <inheritdoc/>
    protected override string? Produce(NodeInputValues inputs)
    {
        string? failure = ResolveTarget(inputs, out string fullPath);
        if (failure is not null)
        {
            return failure;
        }

        if (Directory.Exists(fullPath))
        {
            return $"读取 {Parameters.Path} 失败：目标是目录。";
        }

        if (!IOFile.Exists(fullPath))
        {
            return $"读取 {Parameters.Path} 失败：文件不存在。";
        }

        SetContent(FileContent.Read(fullPath, Parameters.MaxChars));

        return null;
    }
}