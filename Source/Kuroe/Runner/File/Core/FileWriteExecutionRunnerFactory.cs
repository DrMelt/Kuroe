using Kuroe.Files.Shared;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Runner.File;

/// <summary>为文件写入执行建立运行器。</summary>
public sealed class FileWriteExecutionRunnerFactory(
    IWorkspaceFiles files) : IExecutionRunnerFactory
{
    /// <summary>本工厂的注册键。</summary>
    public static ExecutionRunnerKey Key { get; } = new("FileWrite");

    /// <inheritdoc/>
    ExecutionRunnerKey IExecutionRunnerFactory.Key => Key;

    /// <inheritdoc/>
    public IExecutionRunner Create(IExecutionParameters parameters, ExecutionId execution) =>
        parameters is FileExecutionParameters fileParameters
            ? new FileWriteExecutionRunner(fileParameters, files)
            : throw new InvalidOperationException("文件写入执行参数类型不匹配。");
}