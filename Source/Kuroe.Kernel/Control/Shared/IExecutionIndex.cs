using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.State;



namespace Kuroe.Kernel.Control.Shared;

/// <summary>执行号与归属的登记面：建立、恢复、查找与移除执行。</summary>
public interface IExecutionIndex : IExecutionOwnerIndex
{
    /// <summary>为一个任务建立执行并登记归属。</summary>
    IExecution Open(
        ITaskFlowContext owner,
        ExecutionRequest request,
        Func<ExecutionId, ExecutionRequest, IExecution> create);

    /// <summary>登记一个从存档恢复的执行，推进下一次执行号。</summary>
    void Restore(ITaskFlowContext owner, IExecution execution);

    /// <summary>移除一个已清空任务的执行条目。</summary>
    void Remove(ExecutionId id);
}