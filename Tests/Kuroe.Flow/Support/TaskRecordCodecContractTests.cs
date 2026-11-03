using Kuroe.Flow.Persistence.Serialization.Shared;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
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

        Assert.Equal(record.Identity, loaded.Identity);
        Assert.Equal(record.Flow, loaded.Flow);
        Assert.Equal(record.Lifecycle, loaded.Lifecycle);
        Assert.Equal(record.Completed, loaded.Completed);
        Assert.Equal(record.Executions.Count, loaded.Executions.Count);
        Assert.Equal(record.Nodes.Count, loaded.Nodes.Count);
        Assert.Equal(record.Pending.Targets, loaded.Pending.Targets);
    }

    [Fact]
    public void Round_trip_preserves_all_node_record_kinds()
    {
        ITaskRecordCodec codec = CreateCodec();
        TaskRecord record = TaskRecordFixture.Create();

        TaskRecord loaded = codec.Read(codec.Write(record).ThrowIfError()).ThrowIfError();

        Assert.Single(loaded.Nodes.OfType<ExecutableNodeRecord>());
        Assert.Single(loaded.Nodes.OfType<SelfDrivenNodeRecord>());
        Assert.Single(loaded.Nodes.OfType<InputNodeRecord>());
        Assert.Single(loaded.Nodes.OfType<ApprovalNodeRecord>());
        Assert.Single(loaded.Nodes.OfType<ContainerNodeRecord>());
    }

    [Fact]
    public void Round_trip_preserves_nullable_fields()
    {
        ITaskRecordCodec codec = CreateCodec();
        TaskRecord record = TaskRecordFixture.Create();

        TaskRecord loaded = codec.Read(codec.Write(record).ThrowIfError()).ThrowIfError();

        Assert.Equal(record.Executions[1].StartedAt, loaded.Executions[1].StartedAt);
        Assert.Null(Assert.IsType<SelfDrivenNodeRecord>(loaded.Nodes[1]).Common.Publication);
        Assert.Null(loaded.Nodes[1].Common.LastInputGroup);
    }

    [Fact]
    public void Round_trip_preserves_context_source_kinds()
    {
        ITaskRecordCodec codec = CreateCodec();
        TaskRecord record = WithAllContextSources(TaskRecordFixture.Create());

        TaskRecord loaded = codec.Read(codec.Write(record).ThrowIfError()).ThrowIfError();

        Assert.Equal(
            Enum.GetValues<ContextSourceKind>(),
            [.. loaded.Executions[0].Inputs.Select(value => value.Origin.Kind)]);
    }

    [Fact]
    public void Round_trip_preserves_publication_triggers_and_content()
    {
        ITaskRecordCodec codec = CreateCodec();
        TaskRecord record = TaskRecordFixture.Create();

        TaskRecord loaded = codec.Read(codec.Write(record).ThrowIfError()).ThrowIfError();
        PublicationRecord publication = Assert.IsType<ExecutableNodeRecord>(loaded.Nodes[0]).Common.Publication!;

        Assert.False(publication.Triggers[new PortName("结论")]);
        Assert.Equal("计划结果", Assert.Single(publication.Content[new PortName("结论")]).Text);
    }

    [Fact]
    public void Write_is_deterministic()
    {
        ITaskRecordCodec codec = CreateCodec();
        TaskRecord record = TaskRecordFixture.Create();

        Assert.Equal(codec.Write(record).ThrowIfError(), codec.Write(record).ThrowIfError());
    }

    private static TaskRecord WithAllContextSources(TaskRecord record)
    {
        NodePath node = NodePath.Of("整体", "计划");
        PortName port = new("结论");
        ExecutionId execution = new(11);
        ExecutionRecord first = record.Executions[0] with
        {
            Inputs =
            [
                new InputValueRecord(port, new ContextSourceRecord(ContextSourceKind.Execution, execution, node, port), "执行"),
                new InputValueRecord(port, new ContextSourceRecord(ContextSourceKind.Input, null, node, port), "输入"),
                new InputValueRecord(port, new ContextSourceRecord(ContextSourceKind.Start, null, null, port), "开始"),
                new InputValueRecord(port, new ContextSourceRecord(ContextSourceKind.ContainerPort, null, NodePath.Of("整体"), port), "容器"),
                new InputValueRecord(port, new ContextSourceRecord(ContextSourceKind.Default, null, node, port), "默认"),
                new InputValueRecord(port, new ContextSourceRecord(ContextSourceKind.Constant, null, node, port), "常量"),
                new InputValueRecord(port, new ContextSourceRecord(ContextSourceKind.Node, null, node, port), "节点"),
            ],
        };

        return record with { Executions = [first, .. record.Executions.Skip(1)] };
    }
}
