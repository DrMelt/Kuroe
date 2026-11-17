using Kuroe.Files.Shared;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Runner.File;

/// <summary>为文件读取执行建立运行器。</summary>
public sealed class FileReadExecutionRunnerFactory(
    IWorkspaceFiles files) : IExecutionRunnerFactory
{
    /// <inheritdoc/>
    public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) =>
        target.Parameters is FileExecutionParameters parameters
            ? new FileReadExecutionRunner(parameters, files)
            : throw new InvalidOperationException("文件读取执行参数类型不匹配。");
}