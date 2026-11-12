using ErrorOr;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>任务命令对流程宿主的驱动：启动流程运行，暂停、恢复、重新执行与终止经信号或状态迁移落地。由引擎实现。</summary>
public interface IFlowRunner
{
    /// <summary>要求持有任务 Gate：启动该任务的流程运行。</summary>
    void Start(WorkTask task);

    /// <summary>要求持有任务 Gate：按调度种子恢复推进循环，任务保持暂停不自动推进。</summary>
    void Restore(WorkTask task, IReadOnlyList<NodePath> seed);

    /// <summary>要求持有任务 Gate：暂停推进并中断在跑执行。</summary>
    void Pause(WorkTask task);

    /// <summary>要求持有任务 Gate：停止一个已启动的执行。</summary>
    ErrorOr<Success> StopExecution(WorkTask task, GraphExecution execution);

    /// <summary>要求持有任务 Gate：恢复推进循环，continueRestores 为真时恢复可恢复的阻塞。</summary>
    void ResumeTask(WorkTask task, bool continueRestores = true);

    /// <summary>要求持有任务 Gate 之外的调用：给定节点的产出已变，推进它的下游与祖先容器。</summary>
    void Advance(WorkTask task, IReadOnlyList<NodePath> nodes);

    /// <summary>要求持有任务 Gate：对阻塞的执行节点重新执行，返回实际发出的重新执行目标数。</summary>
    int Rerun(WorkTask task);

    /// <summary>要求持有任务 Gate：终止任务并结束流程运行。</summary>
    ErrorOr<Success> Terminate(WorkTask task);

    /// <summary>退出时中断全部任务的推进与执行并等待收口，不改变任务控制状态。</summary>
    Task ShutdownAsync();
}
