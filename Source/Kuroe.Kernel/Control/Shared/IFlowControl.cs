using ErrorOr;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Kernel.Control.Shared;

/// <summary>一个任务的流程控制实例：推进循环、恢复、重新执行与终止。</summary>
public interface IFlowControl
{
    /// <summary>该任务的推进循环。循环结束不等于控制实例不再需要。</summary>
    Task Loop { get; }

    /// <summary>控制实例不再需要时完成。</summary>
    Task Completion { get; }

    /// <summary>是否有步骤停在阻塞。</summary>
    bool HasBlocked { get; }

    /// <summary>是否还有未走完的推进工作。</summary>
    bool HasWork { get; }

    /// <summary>是否有可恢复的阻塞。</summary>
    bool HasRestores { get; }

    /// <summary>启动推进循环。要求持有 <see cref="ITaskFlowContext.Gate"/>。</summary>
    void Start();

    /// <summary>按恢复种子启动推进循环，任务保持暂停不自动推进。要求持有 <see cref="ITaskFlowContext.Gate"/>。</summary>
    void Restore(FlowSeed seed);

    /// <summary>暂停推进并中断在跑执行。要求持有 <see cref="ITaskFlowContext.Gate"/>。</summary>
    void Pause();

    /// <summary>恢复推进循环，<paramref name="continueRestores"/> 为真时恢复可恢复的阻塞。要求持有 <see cref="ITaskFlowContext.Gate"/>。</summary>
    void Resume(bool continueRestores = true);

    /// <summary>终止任务并结束推进循环。要求持有 <see cref="ITaskFlowContext.Gate"/>。</summary>
    ErrorOr<Success> Terminate();

    /// <summary>此刻尚未处理的步骤，供存档捕获。</summary>
    FlowSeed CaptureSeed();

    /// <summary>退出时中断推进，保留任务控制状态与步骤状态。</summary>
    void InterruptForShutdown();
}