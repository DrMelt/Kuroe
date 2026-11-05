using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.File;

/// <summary>文件写入节点的一次执行：把唯一的 Text 输入内容写入文件，写入内容作为产出。</summary>
internal sealed class FileWriteExecutionRunner(ExecutableNode node, FileParameters parameters)
    : FileExecutionRunner(node, parameters)
{
    /// <inheritdoc/>
    public override Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        ExecutionInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ResolveTarget(input, out string fullPath) is { } failure)
        {
            return Task.FromResult(Failure(failure));
        }

        if (Directory.Exists(fullPath))
        {
            return Task.FromResult(Failure($"写入 {Parameters.Path} 失败：目标是目录。"));
        }

        string content = Content(input);
        try
        {
            if (FileContent.Write(fullPath, content, Parameters.MaxChars) is { } writeFailure)
            {
                return Task.FromResult(Failure(writeFailure));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Task.FromResult(Failure("写入", ex));
        }

        Report($"写入 {Parameters.Path}");

        return Task.FromResult<ErrorOr<ExecutionOutput>>(ExecutionOutput.FromContent(content));
    }

    /// <summary>取写入内容端口的内容，路径变量端口不算内容端口。</summary>
    private string Content(ExecutionInput input)
    {
        HashSet<string> variablePorts = [.. Parameters.Variables.Values];
        InputPort? port = Node.Inputs.FirstOrDefault(candidate =>
            PortRules.CarriesText(candidate.Type) && !variablePorts.Contains(candidate.Name.Value));

        return port is { } content ? input.Text(content.Name) ?? string.Empty : string.Empty;
    }
}