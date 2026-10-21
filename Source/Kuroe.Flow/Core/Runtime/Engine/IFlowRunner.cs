using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>任务命令对流程宿主的驱动：启动流程运行，返工与取消经信号恢复或终止。由引擎实现。</summary>
public interface IFlowRunner
{
    /// <summary>要求持有任务 Gate：启动该任务的流程运行。</summary>
    void Start(WorkTask task);

    /// <summary>要求持有任务 Gate 之外的调用：给定节点的产出已变，推进它的下游与祖先容器。</summary>
    void Resume(WorkTask task, IReadOnlyList<NodePath> nodes);

    /// <summary>要求持有任务 Gate：对被阻塞的执行节点再开一轮返工，返回实际发出的重跑目标数。</summary>
    int Rework(WorkTask task);

    /// <summary>要求持有任务 Gate：取消任务并终止流程运行。</summary>
    void Cancel(WorkTask task);

    /// <summary>退出时取消全部任务与流程运行并等待收口。</summary>
    Task ShutdownAsync();
}
