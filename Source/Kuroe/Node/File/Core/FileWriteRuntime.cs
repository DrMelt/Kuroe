using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.File;

/// <summary>文件写入节点的运行期对象：触发齐备且来源有新版本时把唯一的 Text 输入内容写入文件，成功后按声明端口产出。</summary>
internal sealed class FileWriteRuntime(ExecutableNode executable, FileParameters parameters)
    : FileRuntime(executable, parameters)
{
    /// <inheritdoc/>
    protected override string Action => "写入";

    /// <inheritdoc/>
    protected override string? Produce(INodeContext context, int group)
    {
        if (Directory.Exists(Parameters.FullPath))
        {
            return $"写入 {Parameters.Path} 失败：目标是目录。";
        }

        string content = Content(context, group);
        string? failure = FileContent.Write(Parameters.FullPath, content, Parameters.MaxChars);
        if (failure is null)
        {
            SetContent(content);
        }

        return failure;
    }

    /// <summary>取生效组里 Text 输入端口的内容，多个来源值按序拼合。</summary>
    private string Content(INodeContext context, int group)
    {
        FlowEdge? edge = context.Graph.Incoming(Path)
            .FirstOrDefault(edge => edge.Group == group && Ports.CarriesText(edge.Type));
        if (edge is null)
        {
            return string.Empty;
        }

        return string.Join("\n\n", context.Runtime(edge.Source).OutputValues(context, edge.SourcePort)
            .Select(value => value.Text)
            .Where(text => text.Length > 0));
    }
}