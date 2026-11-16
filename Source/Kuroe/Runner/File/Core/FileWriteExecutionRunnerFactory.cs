using Kuroe.Execution.Shared;
using Kuroe.Files.Shared;

namespace Kuroe.Runner.File;

/// <summary>为文件写入执行建立运行器。</summary>
public sealed class FileWriteExecutionRunnerFactory(
    IWorkspaceFiles files) : IExecutionRunnerFactory
{
    /// <inheritdoc/>
    public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) =>
        target.Parameters is FileExecutionParameters parameters
            ? new FileWriteExecutionRunner(parameters, files)
            : throw new InvalidOperationException("文件写入执行参数类型不匹配。");
}