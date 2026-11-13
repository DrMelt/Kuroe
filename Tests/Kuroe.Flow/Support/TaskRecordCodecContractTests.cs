using Kuroe.Flow.Graph.Records.Shared;
using Kuroe.Flow.Node.Shared.Graph.Records;
using Kuroe.Flow.Persistence.Serialization.Shared;
using Kuroe.Flow.Tasks.Records.Shared;
using Kuroe.Flow.Shared.Model;
using Xunit;

namespace Kuroe.Flow.TestSupport;

/// <summary>存档编解码契约：往返、节点多态、空值、出处、错误与稳定输出。</summary>
public abstract class TaskRecordCodecContractTests
{
    protected abstract ITaskRecordCodec CreateCodec();

    [Fact]
    public void Round_trip_preserves_task_record()
    {
        ITaskRecordCodec codec = CreateCodec();
        TaskRecord record = TaskRecordFixture.Create();

        TaskRecord loaded = codec.Read(codec.Write(record).ThrowIfError()).ThrowIfError();
        GraphFlowStateRecord state = State(record);
        GraphFlowStateRecord loadedState = State(loaded);

        Assert.Equal(record.Identity, loaded.Identity);
        Assert.Equal(record.SavedAt, loaded.SavedAt);
        GraphFlowDefinitionRecord definition = Assert.IsType<GraphFlowDefinitionRecord>(record.Flow.Definition);
        Assert.Equal(definition, Assert.IsType<GraphFlowDefinitionRecord>(loaded.Flow.Definition));
        Assert.Equal(record.Lifecycle, loaded.Lifecycle);
        Assert.Equal(record.Completed, loaded.Completed);
        Assert.Equal(record.Executions.Count, loaded.Executions.Count);
        Assert.Equal(state.Nodes.Count, loadedState.Nodes.Count);
        Assert.Equal(state.Ports.Count, loadedState.Ports.Count);
        Assert.Equal(state.Pending.Steps, loadedState.Pending.Steps);
    }

    [Fact]
    public void Round_trip_preserves_all_node_record_kinds()
    {
        ITaskRecordCodec codec = CreateCodec();
        TaskRecord record = TaskRecordFixture.Create();

        TaskRecord loaded = codec.Read(codec.Write(record).ThrowIfError()).ThrowIfError();
        IReadOnlyList<NodeRecord> nodes = State(loaded).Nodes;

        Assert.Equal(2, nodes.OfType<ExecutableNodeRecord>().Count());
        Assert.Single(nodes.OfType<InputNodeRecord>());
        Assert.Single(nodes.OfType<ApprovalNodeRecord>());
        Assert.Single(nodes.OfType<ContainerNodeRecord>());
    }

    [Fact]
    public void Round_trip_preserves_nullable_fields()
    {
        ITaskRecordCodec codec = CreateCodec();
        TaskRecord record = TaskRecordFixture.Create();

        TaskRecord loaded = codec.Read(codec.Write(record).ThrowIfError()).ThrowIfError();
        IReadOnlyList<NodeRecord> nodes = State(record).Nodes;
        IReadOnlyList<NodeRecord> loadedNodes = State(loaded).Nodes;

        Assert.Equal(record.Executions[1].StartedAt, loaded.Executions[1].StartedAt);
        Assert.Null(Assert.IsType<ExecutableNodeRecord>(loadedNodes[1]).Common.ActivePublicationRevision);
        Assert.Null(loadedNodes[1].Common.LastInputGroup);
        Assert.Equal(
            Assert.IsType<ExecutableNodeRecord>(nodes[1]).Block,
            Assert.IsType<ExecutableNodeRecord>(loadedNodes[1]).Block);
    }

    [Fact]
    public void Round_trip_preserves_execution_input_sources()
    {
        ITaskRecordCodec codec = CreateCodec();
        TaskRecord record = TaskRecordFixture.Create();

        TaskRecord loaded = codec.Read(codec.Write(record).ThrowIfError()).ThrowIfError();

        Assert.Equal(
            [.. record.Executions[0].Inputs.Select(value => value.Source)],
            [.. loaded.Executions[0].Inputs.Select(value => value.Source)]);
    }

    [Fact]
    public void Round_trip_preserves_port_history_triggers_and_content()
    {
        ITaskRecordCodec codec = CreateCodec();
        TaskRecord record = TaskRecordFixture.Create();

        TaskRecord loaded = codec.Read(codec.Write(record).ThrowIfError()).ThrowIfError();
        PortPublicationRecord[] ports = [.. State(loaded).Ports.Where(port => port.Node == NodePath.Of("整体", "计划"))];

        Assert.Equal([1L, 2L], ports.Select(port => port.Revision));
        Assert.True(ports[0].Trigger);
        Assert.Equal("旧计划结果", Assert.Single(ports[0].Values).Text);
        Assert.False(ports[1].Trigger);
        Assert.Equal("计划结果", Assert.Single(ports[1].Values).Text);
        Assert.Equal(2, Assert.IsType<ExecutableNodeRecord>(State(loaded).Nodes[0]).Common.ActivePublicationRevision);
    }

    [Fact]
    public void Write_is_deterministic()
    {
        ITaskRecordCodec codec = CreateCodec();
        TaskRecord record = TaskRecordFixture.Create();

        Assert.Equal(codec.Write(record).ThrowIfError(), codec.Write(record).ThrowIfError());
    }

    private static GraphFlowStateRecord State(TaskRecord record) =>
        Assert.IsType<GraphFlowStateRecord>(record.Flow.State);
}
