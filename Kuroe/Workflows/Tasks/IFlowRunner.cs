namespace Kuroe.Workflows.Tasks;

/// <summary>任务命令对流程宿主的驱动：启动流程运行，批准、返工与取消经信号恢复或终止。由引擎实现。</summary>
public interface IFlowRunner
{
    /// <summary>要求持有任务 Gate：启动该任务的流程运行。</summary>
    void Start(WorkTask task);

    /// <summary>要求持有任务 Gate：批准等待放行的节点并发出继续信号，返回被批准的节点数。</summary>
    int Approve(WorkTask task);

    /// <summary>要求持有任务 Gate：对被阻塞的节点再开一轮返工，itemIndex 为空时处理全部，返回实际发出的重跑目标数。</summary>
    int Rework(WorkTask task, int? itemIndex);

    /// <summary>要求持有任务 Gate：取消任务并终止流程运行。</summary>
    void Cancel(WorkTask task);

    /// <summary>退出时取消全部任务与流程运行并等待收口。</summary>
    Task ShutdownAsync();
}