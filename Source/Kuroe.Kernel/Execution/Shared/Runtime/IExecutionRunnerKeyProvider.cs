namespace Kuroe.Execution.Shared.Runtime;

/// <summary>节点类型给出的执行运行器键，执行目标携带它，装配据此选择执行器工厂。</summary>
public interface IExecutionRunnerKeyProvider
{
    /// <summary>本类型使用的执行运行器键。</summary>
    ExecutionRunnerKey RunnerKey { get; }
}