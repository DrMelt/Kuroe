using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Tasks;
using Kuroe.Flow.Tasks.Records;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.RuntimeGraph.Tasks;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.Serialization.Shared.Tests;

/// <summary>通用存档映射：往返、版本、缺失字段、未知节点与错误码。</summary>
public sealed class TaskRecordSerializationMapperTests
{
    [Fact]
    public void Round_trip_preserves_task_record()
    {
        TaskRecord record = TaskRecordFixture.Create();

        TaskRecord loaded = RoundTrip(record);
        GraphFlowStateRecord state = State(record);
        GraphFlowStateRecord loadedState = State(loaded);

        Assert.Equal(record.Identity, loaded.Identity);
        Assert.Equal(record.SavedAt, loaded.SavedAt);
        GraphFlowDefinitionRecord definition = Assert.IsType<GraphFlowDefinitionRecord>(record.Flow.Definition);
        Assert.Equal(definition, Assert.IsType<GraphFlowDefinitionRecord>(loaded.Flow.Definition));
        Assert.Equal(record.Lifecycle, loaded.Lifecycle);
        Assert.Equal(record.Completed, loaded.Completed);
        Assert.Equal(record.Executions.Count, loaded.Executions.Count);
        Assert.Equal(record.Executions[0].Usage, loaded.Executions[0].Usage);
        Assert.Equal(state.Nodes.Count, loadedState.Nodes.Count);
        Assert.Equal(state.Pending.Steps, loadedState.Pending.Steps);
    }

    [Fact]
    public void Round_trip_preserves_node_record_kinds()
    {
        TaskRecord loaded = RoundTrip(TaskRecordFixture.Create());
        IReadOnlyList<NodeRecord> nodes = State(loaded).Nodes;

        Assert.Equal(2, nodes.OfType<ExecutableNodeRecord>().Count());
        Assert.Single(nodes.OfType<InputNodeRecord>());
        Assert.Single(nodes.OfType<ApprovalNodeRecord>());
        Assert.Single(nodes.OfType<ContainerNodeRecord>());
    }

    [Fact]
    public void To_document_uses_current_version()
    {
        TaskRecordDocument document = TaskRecordSerializationMapper
            .ToDocument(TaskRecordFixture.Create())
            .ThrowIfError();

        Assert.Equal(TaskRecordSerializationVersion.Current, document.Version);
    }

    [Fact]
    public void From_document_rejects_unknown_version()
    {
        TaskRecordDocument document = new(999, null);

        Assert.Equal(FlowErrorCodes.RecordVersion, TaskRecordSerializationMapper.FromDocument(document).FirstError.Code);
    }

    [Fact]
    public void From_document_rejects_unsupported_old_version()
    {
        TaskRecordDocument document = Document();
        TaskRecordDocument legacy = document with { Version = TaskRecordSerializationVersion.Current - 1 };

        Assert.Equal(FlowErrorCodes.RecordVersion, TaskRecordSerializationMapper.FromDocument(legacy).FirstError.Code);
    }

    [Fact]
    public void From_document_rejects_missing_task()
    {
        TaskRecordDocument document = new(TaskRecordSerializationVersion.Current, null);

        Assert.Equal(FlowErrorCodes.RecordFormat, TaskRecordSerializationMapper.FromDocument(document).FirstError.Code);
    }

    [Fact]
    public void From_document_rejects_unknown_node_kind()
    {
        TaskRecordDocument document = Document();
        TaskRecordData task = Assert.IsType<TaskRecordData>(document.Task);
        FlowData flow = Assert.IsType<FlowData>(task.Flow);
        GraphFlowStateData state = Assert.IsType<GraphFlowStateData>(flow.GraphState);
        Assert.NotNull(state.Nodes);
        NodeData unknown = state.Nodes[0] with { Kind = "Unknown" };
        TaskRecordDocument changed = document with
        {
            Task = task with
            {
                Flow = flow with { GraphState = state with { Nodes = [unknown, .. state.Nodes.Skip(1)] } },
            },
        };

        Assert.Equal(FlowErrorCodes.RecordFormat, TaskRecordSerializationMapper.FromDocument(changed).FirstError.Code);
    }

    [Fact]
    public void To_document_rejects_unknown_node_record()
    {
        TaskRecord record = TaskRecordFixture.Create();
        NodeRecord unknown = new UnknownNodeRecord(State(record).Nodes[0].Path, State(record).Nodes[0].Common);

        Assert.Equal(
            FlowErrorCodes.RecordWrite,
            TaskRecordSerializationMapper.ToDocument(WithNodes(record, [unknown])).FirstError.Code);
    }

    [Fact]
    public void Round_trip_preserves_code_flow_records()
    {
        Dictionary<string, string> values = new() { ["language"] = "中文" };
        FlowTexts startTexts = new(new Dictionary<PortName, string> { [new PortName("目标")] = "对话" });
        TaskRecord record = new(
            new TaskIdentity(new TaskId(1), "对话", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch),
            DateTimeOffset.UnixEpoch,
            Kuroe.Flow.Tasks.TaskLifecycle.Active,
            false,
            new TaskJournalRecord([], 0),
            new FlowRecord(
                new FlowIdentity(new FlowName("对话"), FlowRecordKinds.Code, "dialogue"),
                new CodeFlowDefinitionRecord("dialogue", values),
                new CodeFlowStateRecord(
                    StartInputs.Single(new PortName("目标"), "对话"),
                    startTexts,
                    values)),
            []);

        TaskRecord loaded = RoundTrip(record);

        CodeFlowDefinitionRecord definition = Assert.IsType<CodeFlowDefinitionRecord>(loaded.Flow.Definition);
        CodeFlowStateRecord state = Assert.IsType<CodeFlowStateRecord>(loaded.Flow.State);

        Assert.Equal("dialogue", definition.Implementation);
        Assert.Equal("中文", definition.Values["language"]);
        Assert.Equal(new FlowName("对话"), loaded.Flow.Identity.Name);
        Assert.Equal(FlowRecordKinds.Code, loaded.Flow.Identity.Kind);
        Assert.Equal("dialogue", loaded.Flow.Identity.Implementation);
        Assert.Equal("中文", state.Values["language"]);
        Assert.Equal("对话", state.Inputs.Text(new PortName("目标")));
        Assert.Equal("对话", state.StartTexts.Text(new PortName("目标")));
    }

    private static TaskRecord RoundTrip(TaskRecord record) =>
        TaskRecordSerializationMapper
            .FromDocument(TaskRecordSerializationMapper.ToDocument(record).ThrowIfError())
            .ThrowIfError();

    private static TaskRecordDocument Document() =>
        TaskRecordSerializationMapper.ToDocument(TaskRecordFixture.Create()).ThrowIfError();

    private static GraphFlowStateRecord State(TaskRecord record) =>
        Assert.IsType<GraphFlowStateRecord>(record.Flow.State);

    private static TaskRecord WithNodes(TaskRecord record, IReadOnlyList<NodeRecord> nodes) =>
        record with { Flow = record.Flow with { State = State(record) with { Nodes = nodes } } };

    private sealed record UnknownNodeRecord(NodePath Path, NodeCommonRecord Common) : NodeRecord(Path, Common);
}
