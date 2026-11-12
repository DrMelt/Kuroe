using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;

namespace Kuroe.Flow.Graph.Records;

/// <summary>端口发布历史的持久形态转换：每次发布按端口展开，恢复时按版本重建。</summary>
public static class PortPublicationRecords
{
    /// <summary>把一个节点的全部发布历史写成端口记录。</summary>
    public static IReadOnlyList<PortPublicationRecord> Capture(
        NodePath node,
        IReadOnlyList<PublishedPorts> history) =>
        [
            .. history.SelectMany(entry => Capture(node, entry)),
        ];

    /// <summary>把端口记录按发布版本还原为发布历史。</summary>
    public static IReadOnlyList<PublishedPorts> Restore(IReadOnlyList<PortPublicationRecord> records) =>
        [
            .. records
                .GroupBy(record => record.Revision)
                .OrderBy(group => group.Key)
                .Select(group => Restore(group.Key, group)),
        ];

    private static IEnumerable<PortPublicationRecord> Capture(NodePath node, PublishedPorts entry)
    {
        Publication publication = entry.Publication;
        foreach (PortName port in publication.Triggers.Keys.Union(publication.Content.Keys))
        {
            yield return new PortPublicationRecord(
                node,
                port,
                publication.Revision,
                entry.At,
                publication.Fires(port),
                [
                    .. publication.Output(port).Select(value => new PublicationValueRecord(
                        RecordSources.Write(value.Origin),
                        value.Text)),
                ]);
        }
    }

    private static PublishedPorts Restore(long revision, IEnumerable<PortPublicationRecord> records)
    {
        PortPublicationRecord[] ports = [.. records];
        PortPublicationRecord first = ports[0];

        return new PublishedPorts(
            new Publication(
                revision,
                ports.ToDictionary(record => record.Port, record => record.Trigger),
                ports
                    .Where(record => record.Values.Count > 0)
                    .ToDictionary(
                        record => record.Port,
                        record => (IReadOnlyList<ContextValue>)
                        [
                            .. record.Values.Select(value => new ContextValue(
                                RecordSources.Read(value.Origin),
                                value.Text)),
                        ])),
            first.At);
    }
}
