using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Runtime.Nodes;

/// <summary>带持久记录的运行时节点基类。节点状态由节点实现捕获与恢复。</summary>
public abstract class RuntimeNode(GraphNode node) : NodeRuntime(node), INodeRecordSource, IPortRecordSource
{
    /// <inheritdoc/>
    public abstract NodeRecord? CaptureRecord();

    /// <inheritdoc/>
    public abstract void RestoreRecord(NodeRecord record, INodeRestoreContext context);

    /// <inheritdoc/>
    public virtual IReadOnlyList<PortPublicationRecord> CapturePorts() =>
        PortPublicationRecords.Capture(Path, Progress.History);

    /// <inheritdoc/>
    public virtual void RestorePorts(IReadOnlyList<PortPublicationRecord> records) =>
        InputProgressRecords.RestorePorts(Progress, records);

    /// <summary>捕获输入侧进展的公共部分。</summary>
    protected NodeCommonRecord CaptureProgress() => InputProgressRecords.Capture(Progress);

    /// <summary>恢复输入侧进展的公共部分。</summary>
    protected void RestoreProgress(NodeCommonRecord record)
    {
        InputProgressRecords.Restore(Progress, record);
        if (record.ActivePublicationRevision is { } revision && Progress.Publication is null)
        {
            if (Progress.History.Count == 0)
            {
                Progress.RestoreEmptyPublication(revision);

                return;
            }

            throw new ArgumentException($"节点 {Path} 的活动发布 {revision} 没有端口历史。");
        }
    }
}
