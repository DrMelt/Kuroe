using Kuroe.Flow.Shared.RuntimeGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;
using IOFile = System.IO.File;

namespace Kuroe.Node.File;

/// <summary>文件读取节点的运行期对象：触发齐备且来源有新版本时读取文件，内容写入唯一的输出端口。</summary>
internal sealed class FileReadRuntime(ExecutableNode executable, FileParameters parameters)
    : FileRuntime(executable, parameters)
{
    /// <inheritdoc/>
    protected override string Action => "读取";

    /// <inheritdoc/>
    protected override string? Produce(INodeContext context, int group)
    {
        if (Directory.Exists(Parameters.FullPath))
        {
            return $"读取 {Parameters.Path} 失败：目标是目录。";
        }

        if (!IOFile.Exists(Parameters.FullPath))
        {
            return $"读取 {Parameters.Path} 失败：文件不存在。";
        }

        SetContent(FileContent.Read(Parameters.FullPath, Parameters.MaxChars));

        return null;
    }
}