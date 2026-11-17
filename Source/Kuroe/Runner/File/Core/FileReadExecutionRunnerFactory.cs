using Kuroe.Files.Shared;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Runner.File;

/// <summary>为文件读取执行建立运行器。</summary>
public sealed class FileReadExecutionRunnerFactory(
    IWorkspaceFiles files) : IExecutionRunnerFactory
{
    /// <summary>本工厂的注册键。</summary>
    public static ExecutionRunnerKey Key { get; } = new("FileRead");

    /// <inheritdoc/>
    ExecutionRunnerKey IExecutionRunnerFactory.Key => Key;

    /// <inheritdoc/>
    public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) =>
        target.Parameters is FileExecutionParameters parameters
            ? new FileReadExecutionRunner(parameters, files)
            : throw new InvalidOperationException("文件读取执行参数类型不匹配。");
}