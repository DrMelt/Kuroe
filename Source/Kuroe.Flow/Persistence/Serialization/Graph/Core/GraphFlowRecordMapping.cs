using Kuroe.Flow.Execution;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Node.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Tasks;

namespace Kuroe.Flow.Persistence.Graph;

/// <summary>图流程记录到存档形状的映射。</summary>
internal static class GraphFlowRecordMapping
{
    public static GraphFlowDefinitionData ToData(GraphFlowDefinitionRecord definition) =>
        new(definition.Name.Value, definition.Nodes, definition.Flow);

    public static GraphFlowStateData ToData(GraphFlowStateRecord state) =>
        new(
            new TaskStartData(
                [.. state.Start.Inputs.Values.Select(input => new StartInputData(input.Port.Value, input.Text))],
                ToData(state.Start.StartTexts)),
            [.. state.Nodes.Select(ToData)],
            [.. state.Ports.Select(ToData)],
            new SchedulerSeedData([.. state.Pending.Steps.Select(step => ToData(NodePath.Of([.. step.Levels])))]));

    private static InputValueData ToData(InputValueRecord value) =>
        new(value.Port.Value, ToData(value.Origin), value.Text);

    private static ContextSourceData ToData(ContextSourceRecord source) =>
        new(
            source.Kind,
            source.Execution?.Value,
            source.Node is { } node ? ToData(node) : null,
            source.Port?.Value);

    private static NodeData ToData(NodeRecord node) =>
        node switch
        {
            ExecutableNodeRecord executable => new NodeData(
                Kind: "Executable",
                Path: ToData(executable.Path),
                Common: ToData(executable.Common),
                Block: ToData(executable.Block),
                RerunTargets: [.. executable.RerunTargets.Select(ToData)],
                Waiting: null,
                Inputs: null,
                Awaiting: null),

            InputNodeRecord input => new NodeData(
                Kind: "Input",
                Path: ToData(input.Path),
                Common: ToData(input.Common),
                Block: null,
                RerunTargets: null,
                Waiting: input.Waiting,
                Inputs: null,
                Awaiting: null),
            ApprovalNodeRecord approval => new NodeData(
                Kind: "Approval",
                Path: ToData(approval.Path),
                Common: ToData(approval.Common),
                Block: null,
                RerunTargets: null,
                Waiting: null,
                Inputs: ToData(approval.Inputs),
                Awaiting: approval.Awaiting),
            ContainerNodeRecord container => new NodeData(
                Kind: "Container",
                Path: ToData(container.Path),
                Common: ToData(container.Common),
                Block: null,
                RerunTargets: null,
                Waiting: null,
                Inputs: null,
                Awaiting: null),
            _ => throw new NotSupportedException($"不认识的节点记录 {node.GetType().Name}。"),
        };

    private static NodeCommonData ToData(NodeCommonRecord common) =>
        new(
            common.Revision,
            common.Executions,
            common.LastInputGroup,
            [.. common.Consumed.Select(entry => new ConsumedRevisionData(
                entry.Source is { } source ? ToData(source) : null,
                entry.Port?.Value,
                entry.Revision))],
            common.ActivePublicationRevision,
            common.ForceRerun);

    private static PortPublicationData ToData(PortPublicationRecord record) =>
        new(
            ToData(record.Node),
            record.Port.Value,
            record.Revision,
            record.At,
            record.Trigger,
            [.. record.Values.Select(value => new PublicationValueData(ToData(value.Origin), value.Text))]);

    private static NodeBlockData? ToData(NodeBlock? block) =>
        block is null
            ? null
            : new NodeBlockData(block.Summary, block.Execution?.Value, block.At);

    private static NodeInputValuesData ToData(NodeInputValuesRecord inputs) =>
        new(
            [.. inputs.Ordered.Select(item => new NodeInputValueData(
                item.Target.Value,
                item.Type,
                ToData(item.Value)))]);

    private static NodePathData ToData(NodePath path) =>
        new([.. path.Levels.Select(level => level.Value)]);

    private static Dictionary<string, string> ToData(IReadOnlyDictionary<PortName, string> values) =>
        values.ToDictionary(pair => pair.Key.Value, pair => pair.Value);
}