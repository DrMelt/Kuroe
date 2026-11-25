using Kuroe.Files.Shared;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Host.Ability.Runner.File;

/// <summary>为文件读取执行建立运行器。</summary>
public sealed class FileReadExecutionRunnerFactory(
    IWorkspaceFiles files,
    TimeProvider time) : IExecutionRunnerFactory
{
    /// <inheritdoc/>
    public IExecutionRunner Create(
        IExecutionParameters parameters,
        IExecutionRequestEntry requests) =>
        parameters is FileExecutionParameters fileParameters
            ? new FileReadExecutionRunner(fileParameters, files, requests, time)
            : throw new InvalidOperationException("文件读取执行参数类型不匹配。");
}