namespace Kuroe.Execution.Shared;

/// <summary>按执行目标建立运行器。执行运行器项目实现它，种类由装配处给出。</summary>
public interface IExecutionRunnerFactory
{
    /// <summary>执行种类，用于选择运行器。</summary>
    string Kind { get; }

    /// <summary>为一次执行建立运行器。</summary>
    IExecutionRunner Create(IExecutionTarget target, ExecutionId execution);
}