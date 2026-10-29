namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>按执行号建立一次执行的运行器。替换实现即可在不接执行器的情况下验证流程推进。</summary>
public interface IExecutionRunnerFactory
{
    /// <summary>为一次执行建立运行器，过程记录随运行器一同建立。</summary>
    IExecutionRunner Create(ExecutionId execution);
}