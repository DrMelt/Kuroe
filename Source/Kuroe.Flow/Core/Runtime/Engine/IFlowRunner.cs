using ErrorOr;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>任务命令对流程宿主的驱动：启动流程运行，暂停、恢复、返工与终止经信号或状态迁移落地。由引擎实现。</summary>
public interface IFlowRunner
{
    /// <summary>要求持有任务 Gate：启动该任务的流程运行。</summary>
    void Start(WorkTask task);

    /// <summary>要求持有任务 Gate：按调度种子恢复推进循环，任务保持暂停不自动推进。</summary>
    void Restore(WorkTask task, IReadOnlyList<NodePath> seed);

    /// <summary>要求持有任务 Gate：暂停推进并中断在跑执行。</summary>
    void Pause(WorkTask task);

    /// <summary>要求持有任务 Gate：恢复暂停的推进循环。</summary>
    void ResumeTask(WorkTask task);

    /// <summary>要求持有任务 Gate 之外的调用：给定节点的产出已变，推进它的下游与祖先容器。</summary>
    void Advance(WorkTask task, IReadOnlyList<NodePath> nodes);

    /// <summary>要求持有任务 Gate：对被阻塞的执行节点再开一轮返工，返回实际发出的重跑目标数。</summary>
    int Rework(WorkTask task);

    /// <summary>要求持有任务 Gate：终止任务并结束流程运行。</summary>
    ErrorOr<Success> Cancel(WorkTask task);

    /// <summary>退出时中断全部任务的推进与执行并等待收口，不改变任务控制状态。</summary>
    Task ShutdownAsync();
}
