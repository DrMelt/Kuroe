namespace Kuroe.Flow.Execution;

/// <summary>按执行种类选择运行器工厂。</summary>
public interface IExecutionRunnerSource
{
    /// <summary>为执行目标建立运行器。</summary>
    IExecutionRunner Create(IExecutionTarget target, ExecutionId execution);
}