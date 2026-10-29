using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Sessions;

namespace Kuroe.Node.Llm.Executions;

/// <summary>为 Llm 执行节点建立单次执行的运行器。</summary>
public sealed class ExecutionRunnerFactory(
    SessionFactory sessions,
    IExecutionStatusReader status) : IExecutionRunnerFactory
{
    /// <summary>为一次执行建立运行器，过程记录随运行器一同建立。</summary>
    public IExecutionRunner Create(ExecutionId execution) =>
        new ExecutionRunner(sessions, status, execution);
}