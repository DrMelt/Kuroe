using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Shared;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.File;

/// <summary>文件节点的一次执行：保存文件声明，路径解析与失败说明由具体节点复用。</summary>
internal abstract class FileExecutionRunner(ExecutableNode node, FileParameters parameters) : IExecutionRunner
{
    private readonly ExecutionJournal _journal = new();

    /// <summary>本次执行所属的执行节点定义。</summary>
    protected ExecutableNode Node { get; } = node;

    /// <summary>本节点的类型专属声明。</summary>
    protected FileParameters Parameters { get; } = parameters;

    /// <summary>本次执行的过程记录。</summary>
    public IProcessJournal Journal => _journal;

    /// <summary>执行一次文件读写，成功时给出内容。</summary>
    public abstract Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        ExecutionInput input,
        CancellationToken cancellationToken);

    /// <summary>按路径变量解析文件路径，成功时给出工作区内的绝对路径。</summary>
    protected string? ResolveTarget(ExecutionInput input, out string fullPath)
    {
        fullPath = string.Empty;

        ErrorOr<PathResolution> resolved = PathVariables.Resolve(
            Parameters.Variables,
            port => input.Text(new PortName(port)),
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

    /// <summary>记下执行进展。</summary>
    protected void Report(string text) => _journal.Append("文件", text);

    /// <summary>记下失败原因并给出失败的执行结果。</summary>
    protected ErrorOr<ExecutionOutput> Failure(string message)
    {
        _journal.Append("文件", message, JournalLevel.Warning);

        return Error.Failure("File.Failed", message);
    }

    /// <summary>把文件系统异常转成失败结果，保留旧的失败说明格式。</summary>
    protected ErrorOr<ExecutionOutput> Failure(string action, Exception exception) =>
        Failure($"{action} {Parameters.Path} 失败：{exception.Message}");
}