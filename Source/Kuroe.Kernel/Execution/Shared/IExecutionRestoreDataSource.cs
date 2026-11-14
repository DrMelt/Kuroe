namespace Kuroe.Execution.Shared;

/// <summary>运行器给出本次执行的恢复负载。成功收口也给出，是否用于恢复由任务编排决定。流程层只保存与转发，不解释负载。</summary>
public interface IExecutionRestoreDataSource
{
    /// <summary>本次执行的恢复负载，不产生时为空。</summary>
    ExecutionRestoreState? RestoreData { get; }

    /// <summary>运行中恢复负载变化后触发，收口时的最终负载由流程层在收口读取。</summary>
    event Action? RestoreDataChanged;
}