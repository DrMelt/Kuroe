namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>运行器在无法完成但可以恢复时给出恢复状态。引擎只保存与转发，不解释负载。</summary>
public interface IExecutionRestoreStateReader
{
    /// <summary>可供后续恢复的状态，不可恢复时为空。</summary>
    ExecutionRestoreState? RestoreState { get; }

    /// <summary>恢复状态更新后触发。</summary>
    event Action? RestoreStateChanged;
}