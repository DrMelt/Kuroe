using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>输入侧进展的持久形态转换：版本、执行次数、上次启动组、已消费版本、重跑意图与发布一次往返。</summary>
public static class InputProgressCheckpoints
{
    /// <summary>把输入侧进展写成公共检查点。</summary>
    public static NodeCommonCheckpoint Capture(InputProgress progress) =>
        new(
            progress.Revision,
            progress.Executions,
            progress.LastInputGroup,
            [.. progress.Consumed.Select(pair => new ConsumedRevisionCheckpoint(pair.Key.Source, pair.Key.Port, pair.Value))],
            PublicationCheckpoints.Capture(progress.Publication),
            progress.ForceRerun);

    /// <summary>按公共检查点还原输入侧进展。</summary>
    public static void Restore(InputProgress progress, NodeCommonCheckpoint checkpoint) =>
        progress.Restore(
            checkpoint.Revision,
            checkpoint.Executions,
            checkpoint.LastInputGroup,
            checkpoint.ForceRerun,
            checkpoint.Consumed.Select(entry => new KeyValuePair<ConsumptionKey, long>(
                new ConsumptionKey(entry.Source, entry.Port),
                entry.Revision)),
            PublicationCheckpoints.Restore(checkpoint.Publication, checkpoint.Revision));
}