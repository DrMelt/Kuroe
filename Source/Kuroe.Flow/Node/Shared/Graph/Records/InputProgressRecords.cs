using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Node.Shared.Graph.Records;

/// <summary>输入侧进展的持久形态转换：版本、执行次数、上次启动组、已消费版本、重跑意图与活动发布版本。</summary>
public static class InputProgressRecords
{
    /// <summary>把输入侧进展写成公共持久记录。</summary>
    public static NodeCommonRecord Capture(InputProgress progress) =>
        new(
            progress.Revision,
            progress.Executions,
            progress.LastInputGroup,
            [.. progress.Consumed.Select(pair => new ConsumedRevisionRecord(pair.Key.Source, pair.Key.Port, pair.Value))],
            progress.Publication?.Revision,
            progress.ForceRerun);

    /// <summary>恢复端口发布历史，不改变当前活动发布。</summary>
    public static void RestorePorts(InputProgress progress, IReadOnlyList<PortPublicationRecord> records) =>
        progress.RestoreHistory(PortPublicationRecords.Restore(records));

    /// <summary>按公共持久记录还原输入侧进展与活动发布。</summary>
    public static void Restore(InputProgress progress, NodeCommonRecord record) =>
        progress.Restore(
            record.Revision,
            record.Executions,
            record.LastInputGroup,
            record.ForceRerun,
            record.Consumed.Select(entry => new KeyValuePair<ConsumptionKey, long>(
                new ConsumptionKey(entry.Source, entry.Port),
                entry.Revision)),
            record.ActivePublicationRevision);
}
