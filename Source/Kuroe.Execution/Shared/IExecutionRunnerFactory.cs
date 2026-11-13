namespace Kuroe.Execution.Shared;

/// <summary>按执行目标建立运行器。节点类型实现它来提供自身运行器。</summary>
public interface IExecutionRunnerFactory
{
    /// <summary>执行种类，用于选择运行器。</summary>
    string Kind { get; }

    /// <summary>为一次执行建立运行器。</summary>
    IExecutionRunner Create(IExecutionTarget target, ExecutionId execution);
}