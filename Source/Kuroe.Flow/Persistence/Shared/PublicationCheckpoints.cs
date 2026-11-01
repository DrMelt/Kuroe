using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>发布状态的持久形态转换：触发表与内容随进展一并保存，恢复时直接还原，不由成员或执行状态重建。</summary>
public static class PublicationCheckpoints
{
    /// <summary>把一次发布写成持久形态，没有发布时为空。</summary>
    public static PublicationCheckpoint? Capture(Publication? publication) =>
        publication is null
            ? null
            : new PublicationCheckpoint(
                publication.Triggers,
                publication.Content.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<PublicationValueCheckpoint>)
                    [
                        .. pair.Value.Select(value => new PublicationValueCheckpoint(
                            CheckpointSources.Write(value.Origin),
                            value.Text)),
                    ]));

    /// <summary>从持久形态还原一次发布，版本取节点进展的版本。</summary>
    public static Publication? Restore(PublicationCheckpoint? checkpoint, long revision) =>
        checkpoint is null
            ? null
            : new Publication(
                revision,
                checkpoint.Triggers,
                checkpoint.Content.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<ContextValue>)
                    [
                        .. pair.Value.Select(value => new ContextValue(
                            CheckpointSources.Read(value.Origin),
                            value.Text)),
                    ]));
}
