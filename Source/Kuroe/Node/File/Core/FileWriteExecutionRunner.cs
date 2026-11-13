using ErrorOr;
using Kuroe.Execution.Shared;
using Kuroe.Files.Shared;

namespace Kuroe.Node.File;

/// <summary>文件写入节点的一次执行：把唯一的 Text 输入内容写入文件，写入内容作为产出。</summary>
internal sealed class FileWriteExecutionRunner(
    FileParameters parameters,
    IWorkspaceFiles files)
    : FileExecutionRunner(parameters, files)
{
    /// <inheritdoc/>
    public override Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        ExecutionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ResolveTarget(request, out string fullPath) is { } failure)
        {
            return Task.FromResult(Failure(failure));
        }

        if (Files.DirectoryExists(fullPath))
        {
            return Task.FromResult(Failure($"写入 {Parameters.Path} 失败：目标是目录。"));
        }

        string content = Content(request);
        try
        {
            if (FileContent.Write(Files, fullPath, content, Parameters.MaxChars) is { } writeFailure)
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
    private string Content(ExecutionRequest request)
    {
        HashSet<string> variablePorts = [.. Parameters.Variables.Values];
        ExecutionInputPort? port = request.InputPorts.FirstOrDefault(candidate =>
            candidate.Kind == ExecutionPortKind.Text && !variablePorts.Contains(candidate.Name.Value));

        return port is { } content ? request.Texts.GetValueOrDefault(content.Name) ?? string.Empty : string.Empty;
    }
}