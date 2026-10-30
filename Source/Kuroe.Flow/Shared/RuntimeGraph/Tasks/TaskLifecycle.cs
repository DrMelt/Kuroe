namespace Kuroe.Flow.Shared.RuntimeGraph.Tasks;

/// <summary>任务的控制状态。对外展示的任务状态由控制状态、执行与节点状态汇总得出。</summary>
public enum TaskLifecycle
{
    /// <summary>正常推进。</summary>
    Active,

    /// <summary>已暂停，不再启动新的执行，可恢复。</summary>
    Paused,

    /// <summary>已终止，不再推进。</summary>
    Terminated,
}
