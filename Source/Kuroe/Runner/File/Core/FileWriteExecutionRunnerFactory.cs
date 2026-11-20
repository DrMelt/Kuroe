using Kuroe.Files.Shared;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Runner.File;

/// <summary>为文件写入执行建立运行器。</summary>
public sealed class FileWriteExecutionRunnerFactory(
    IWorkspaceFiles files) : IExecutionRunnerFactory
{
    /// <inheritdoc/>
    public IExecutionRunner Create(
        IExecutionParameters parameters,
        IExecutionRequestEntry requests) =>
        parameters is FileExecutionParameters fileParameters
            ? new FileWriteExecutionRunner(fileParameters, files, requests)
            : throw new InvalidOperationException("文件写入执行参数类型不匹配。");
}