using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;
using IOFile = System.IO.File;

namespace Kuroe.Node.File;

/// <summary>文件读取节点的一次执行：按路径变量解析路径并读取文件，内容作为产出。</summary>
internal sealed class FileReadExecutionRunner(ExecutableNode node, FileParameters parameters)
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
            return Task.FromResult(Failure($"读取 {Parameters.Path} 失败：目标是目录。"));
        }

        if (!IOFile.Exists(fullPath))
        {
            return Task.FromResult(Failure($"读取 {Parameters.Path} 失败：文件不存在。"));
        }

        string content;
        try
        {
            content = FileContent.Read(fullPath, Parameters.MaxChars);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Task.FromResult(Failure("读取", ex));
        }

        Report($"读取 {Parameters.Path}");

        return Task.FromResult<ErrorOr<ExecutionOutput>>(ExecutionOutput.FromContent(content));
    }
}