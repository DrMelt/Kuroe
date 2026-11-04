using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
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

        Assert.Equal(record.Identity, loaded.Identity);
        Assert.Equal(record.SavedAt, loaded.SavedAt);
        Assert.Equal(record.Flow, loaded.Flow);
        Assert.Equal(record.Lifecycle, loaded.Lifecycle);
        Assert.Equal(record.Completed, loaded.Completed);
        Assert.Equal(record.Executions.Count, loaded.Executions.Count);
        Assert.Equal(record.Nodes.Count, loaded.Nodes.Count);
        Assert.Equal(record.Pending.Targets, loaded.Pending.Targets);
    }

    [Fact]
    public void Round_trip_preserves_node_record_kinds()
    {
        TaskRecord loaded = RoundTrip(TaskRecordFixture.Create());

        Assert.Single(loaded.Nodes.OfType<ExecutableNodeRecord>());
        Assert.Single(loaded.Nodes.OfType<SelfDrivenNodeRecord>());
        Assert.Single(loaded.Nodes.OfType<InputNodeRecord>());
        Assert.Single(loaded.Nodes.OfType<ApprovalNodeRecord>());
        Assert.Single(loaded.Nodes.OfType<ContainerNodeRecord>());
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
    public void From_document_reads_version_five_block_fields()
    {
        TaskRecordDocument current = Document();
        TaskRecordData task = Assert.IsType<TaskRecordData>(current.Task);
        List<NodeData> source = task.Nodes ?? [];
        List<NodeData> nodes =
        [
            source[0] with { Block = null, Blocked = true },
            source[1] with { Block = null, Failure = "读取失败。" },
            .. source.Skip(2),
        ];
        TaskRecordDocument legacy = current with
        {
            Version = 5,
            Task = task with { SavedAt = null, Nodes = nodes },
        };

        TaskRecord loaded = TaskRecordSerializationMapper.FromDocument(legacy).ThrowIfError();
        TaskIdentityData identity = Assert.IsType<TaskIdentityData>(task.Identity);
        Assert.Equal(identity.LastActivityAt, loaded.SavedAt);

        NodeBlock executable = Assert.IsType<NodeBlock>(
            Assert.IsType<ExecutableNodeRecord>(loaded.Nodes[0]).Block);
        Assert.Equal("存档未记录阻塞原因。", executable.Summary);

        NodeBlock selfDriven = Assert.IsType<NodeBlock>(
            Assert.IsType<SelfDrivenNodeRecord>(loaded.Nodes[1]).Block);
        Assert.Equal("读取失败。", selfDriven.Summary);
    }

    [Fact]
    public void From_document_ignores_legacy_block_fields_at_current_version()
    {
        TaskRecordDocument document = Document();
        TaskRecordData task = Assert.IsType<TaskRecordData>(document.Task);
        List<NodeData> source = task.Nodes ?? [];
        List<NodeData> nodes =
        [
            source[0] with { Block = null, Blocked = true },
            source[1] with { Block = null, Failure = "读取失败。" },
            .. source.Skip(2),
        ];
        TaskRecordDocument changed = document with
        {
            Task = task with { Nodes = nodes },
        };

        TaskRecord loaded = TaskRecordSerializationMapper.FromDocument(changed).ThrowIfError();

        Assert.Null(Assert.IsType<ExecutableNodeRecord>(loaded.Nodes[0]).Block);
        Assert.Null(Assert.IsType<SelfDrivenNodeRecord>(loaded.Nodes[1]).Block);
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
        Assert.NotNull(task.Nodes);
        NodeData unknown = task.Nodes[0] with { Kind = "Unknown" };
        TaskRecordDocument changed = document with
        {
            Task = task with { Nodes = [unknown, .. task.Nodes.Skip(1)] },
        };

        Assert.Equal(FlowErrorCodes.RecordFormat, TaskRecordSerializationMapper.FromDocument(changed).FirstError.Code);
    }

    [Fact]
    public void To_document_rejects_unknown_node_record()
    {
        TaskRecord record = TaskRecordFixture.Create();
        NodeRecord unknown = new UnknownNodeRecord(record.Nodes[0].Path, record.Nodes[0].Common);

        Assert.Equal(
            FlowErrorCodes.RecordWrite,
            TaskRecordSerializationMapper.ToDocument(record with { Nodes = [unknown] }).FirstError.Code);
    }

    private static TaskRecord RoundTrip(TaskRecord record) =>
        TaskRecordSerializationMapper
            .FromDocument(TaskRecordSerializationMapper.ToDocument(record).ThrowIfError())
            .ThrowIfError();

    private static TaskRecordDocument Document() =>
        TaskRecordSerializationMapper.ToDocument(TaskRecordFixture.Create()).ThrowIfError();

    private sealed record UnknownNodeRecord(NodePath Path, NodeCommonRecord Common) : NodeRecord(Path, Common);
}
