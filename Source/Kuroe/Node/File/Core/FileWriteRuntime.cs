using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.File;

/// <summary>文件写入节点的运行期对象：触发齐备且来源有新版本时把唯一的 Text 输入内容写入文件，成功后按声明端口产出。</summary>
internal sealed class FileWriteRuntime(ExecutableNode executable, FileParameters parameters)
    : FileRuntime(executable, parameters)
{
    /// <inheritdoc/>
    protected override string Action => "写入";

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
            return $"写入 {Parameters.Path} 失败：目标是目录。";
        }

        string content = Content(inputs);
        string? writeFailure = FileContent.Write(fullPath, content, Parameters.MaxChars);
        if (writeFailure is null)
        {
            SetContent(content);
        }

        return writeFailure;
    }

    /// <summary>取写入内容端口的内容，路径变量端口不算内容端口。</summary>
    private string Content(NodeInputValues inputs)
    {
        HashSet<string> variablePorts = [.. Parameters.Variables.Values];
        InputPort? port = Executable.Inputs.FirstOrDefault(candidate =>
            PortRules.CarriesText(candidate.Type) && !variablePorts.Contains(candidate.Name.Value));

        return port is { } content ? inputs.Text(content.Name) ?? string.Empty : string.Empty;
    }
}