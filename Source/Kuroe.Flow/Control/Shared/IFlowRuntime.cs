using ErrorOr;
using Kuroe.Flow.Execution.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Control.Shared;

/// <summary>任务命令对流程运行期的驱动：启动流程运行，暂停、恢复、重新执行与终止经控制实例落地。</summary>
public interface IFlowRuntime
{
    /// <summary>要求持有任务 Gate：启动该任务的流程运行。</summary>
    void Start(ITaskFlowHost task);

    /// <summary>要求持有任务 Gate：按恢复种子启动推进循环，任务保持暂停不自动推进。</summary>
    void Restore(ITaskFlowHost task, FlowSeed seed);

    /// <summary>要求持有任务 Gate：暂停推进并中断在跑执行。</summary>
    void Pause(ITaskFlowContext task);

    /// <summary>要求持有任务 Gate：停止一个已启动的执行。</summary>
    ErrorOr<Success> StopExecution(ITaskFlowContext task, ExecutionId id);

    /// <summary>要求持有任务 Gate：恢复推进循环，continueRestores 为真时恢复可恢复的阻塞。</summary>
    void ResumeTask(ITaskFlowContext task, bool continueRestores = true);

    /// <summary>要求持有任务 Gate 之外的调用：给定步骤的产出已变，推进它的下游与祖先容器。</summary>
    void Advance(ITaskFlowContext task, IReadOnlyList<FlowStepPath> steps);

    /// <summary>要求持有任务 Gate：对阻塞的步骤重新执行，返回实际发出的重新执行目标数。</summary>
    int Rerun(ITaskFlowContext task);

    /// <summary>要求持有任务 Gate：终止任务并结束流程运行。</summary>
    ErrorOr<Success> Terminate(ITaskFlowContext task);

    /// <summary>退出时中断全部任务的推进与执行并等待收口，不改变任务控制状态。</summary>
    Task ShutdownAsync();
}