using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>按时间有序的唯一键生成任务标识，进程间不重复。</summary>
public sealed class GuidTaskIdSource : ITaskIdSource
{
    /// <inheritdoc/>
    public TaskId Next() => TaskId.New();
}
