using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
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
        Assert.Equal(record.Executions[0].Usage, loaded.Executions[0].Usage);
        Assert.Equal(record.Nodes.Count, loaded.Nodes.Count);
        Assert.Equal(record.Pending.Targets, loaded.Pending.Targets);
    }

    [Fact]
    public void Round_trip_preserves_node_record_kinds()
    {
        TaskRecord loaded = RoundTrip(TaskRecordFixture.Create());

        Assert.Equal(2, loaded.Nodes.OfType<ExecutableNodeRecord>().Count());
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
