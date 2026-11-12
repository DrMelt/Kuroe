using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using ExecutionId = Kuroe.Flow.Shared.RuntimeGraph.Execution.ExecutionId;

namespace Kuroe.Flow.Control;

/// <summary>一个任务的流程控制实例：推进循环、恢复、重新执行、终止与执行停止。任务聚合只持有它，命令经运行期引擎转发。</summary>
public interface IFlowControl
{
    /// <summary>该任务的推进循环，宿主持有以便收口。</summary>
    Task Loop { get; }

    /// <summary>启动推进循环。要求持有任务 Gate。</summary>
    void Start();

    /// <summary>按调度种子恢复推进循环，任务保持暂停不自动推进。要求持有任务 Gate。</summary>
    void Restore(IReadOnlyList<FlowStepPath> seed);

    /// <summary>暂停推进并中断在跑执行。要求持有任务 Gate。</summary>
    void Pause();

    /// <summary>给定节点的产出已变，推进它的下游与祖先容器。要求持有任务 Gate 之外的调用。</summary>
    void Advance(IReadOnlyList<FlowStepPath> nodes);

    /// <summary>恢复推进循环，continueRestores 为真时恢复可恢复的阻塞。要求持有任务 Gate。</summary>
    void Resume(bool continueRestores = true);

    /// <summary>对阻塞的执行节点重新执行，返回实际发出的重新执行目标数。要求持有任务 Gate。</summary>
    int Rerun();

    /// <summary>停止一个已启动的执行。要求持有任务 Gate。</summary>
    ErrorOr<Success> StopExecution(ExecutionId id);

    /// <summary>终止任务并结束推进循环。要求持有任务 Gate。</summary>
    ErrorOr<Success> Terminate();

    /// <summary>此刻尚未处理的目标，供存档捕获。</summary>
    SchedulerSeed CaptureSeed();

    /// <summary>退出时中断推进，保留任务控制状态与节点状态。</summary>
    void InterruptForShutdown();
}