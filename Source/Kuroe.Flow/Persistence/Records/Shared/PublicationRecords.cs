using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;

namespace Kuroe.Flow.Persistence.Records.Shared;

/// <summary>发布状态的持久形态转换：触发表与内容随进展一并保存，恢复时直接还原，不由成员或执行状态重建。</summary>
public static class PublicationRecords
{
    /// <summary>把一次发布写成持久形态，没有发布时为空。</summary>
    public static PublicationRecord? Capture(Publication? publication) =>
        publication is null
            ? null
            : new PublicationRecord(
                publication.Triggers,
                publication.Content.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<PublicationValueRecord>)
                    [
                        .. pair.Value.Select(value => new PublicationValueRecord(
                            RecordSources.Write(value.Origin),
                            value.Text)),
                    ]));

    /// <summary>从持久形态还原一次发布，版本取节点进展的版本。</summary>
    public static Publication? Restore(PublicationRecord? record, long revision) =>
        record is null
            ? null
            : new Publication(
                revision,
                record.Triggers,
                record.Content.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<ContextValue>)
                    [
                        .. pair.Value.Select(value => new ContextValue(
                            RecordSources.Read(value.Origin),
                            value.Text)),
                    ]));
}
