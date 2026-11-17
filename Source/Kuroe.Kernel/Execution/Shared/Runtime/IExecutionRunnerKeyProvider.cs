namespace Kuroe.Execution.Shared.Runtime;

/// <summary>节点类型给出的执行运行器键，执行目标据此选择运行器。</summary>
public interface IExecutionRunnerKeyProvider
{
    /// <summary>本类型使用的执行运行器键。</summary>
    ExecutionRunnerKey RunnerKey { get; }
}