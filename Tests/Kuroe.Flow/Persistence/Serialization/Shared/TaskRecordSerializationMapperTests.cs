using Kuroe.Flow.Tasks.Shared;
using System.Text.Json;
using Kuroe.Flow.Tasks.Records.Shared;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.Serialization.Shared.Tests;

/// <summary>通用存档映射：往返、版本、缺失字段、编解码器选择与错误码。</summary>
public sealed class TaskRecordSerializationMapperTests
{
    private static readonly TaskRecordSerializationMapper Mapper = new([new FakeCodec()]);

    [Fact]
    public void Round_trip_preserves_task_record()
    {
        TaskRecord record = Record();

        TaskRecord loaded = RoundTrip(record);

        Assert.Equal(record.Identity, loaded.Identity);
        Assert.Equal(record.SavedAt, loaded.SavedAt);
        Assert.Equal(new FakeDefinition("定义"), loaded.Flow.Definition);
        Assert.Equal(new FakeState("状态"), loaded.Flow.State);
        Assert.Equal(record.Lifecycle, loaded.Lifecycle);
        Assert.Equal(record.Completed, loaded.Completed);
        Assert.Equal(record.Executions.Count, loaded.Executions.Count);
        Assert.Equal(record.Executions[0].Usage, loaded.Executions[0].Usage);
    }

    [Fact]
    public void To_document_uses_current_version()
    {
        TaskRecordDocument document = Mapper.ToDocument(Record()).ThrowIfError();

        Assert.Equal(TaskRecordSerializationVersion.Current, document.Version);
    }

    [Fact]
    public void From_document_rejects_unknown_version()
    {
        TaskRecordDocument document = new(999, null);

        Assert.Equal(FlowErrorCodes.RecordVersion, Mapper.FromDocument(document).FirstError.Code);
    }

    [Fact]
    public void From_document_rejects_unsupported_old_version()
    {
        TaskRecordDocument document = Document();
        TaskRecordDocument legacy = document with { Version = TaskRecordSerializationVersion.Current - 1 };

        Assert.Equal(FlowErrorCodes.RecordVersion, Mapper.FromDocument(legacy).FirstError.Code);
    }

    [Fact]
    public void From_document_rejects_missing_task()
    {
        TaskRecordDocument document = new(TaskRecordSerializationVersion.Current, null);

        Assert.Equal(FlowErrorCodes.RecordFormat, Mapper.FromDocument(document).FirstError.Code);
    }

    [Fact]
    public void To_document_rejects_an_unregistered_flow()
    {
        TaskRecord record = Record() with
        {
            Flow = new FlowRecord(
                new FlowIdentity(new FlowName("未知"), "unknown", "unknown"),
                new FakeDefinition("定义"),
                new FakeState("状态")),
        };

        Assert.Equal(FlowErrorCodes.RecordWrite, Mapper.ToDocument(record).FirstError.Code);
    }

    [Fact]
    public void From_document_rejects_an_unregistered_flow()
    {
        TaskRecordDocument document = Document();
        TaskRecordData task = Assert.IsType<TaskRecordData>(document.Task);
        FlowData flow = Assert.IsType<FlowData>(task.Flow);
        TaskRecordDocument changed = document with
        {
            Task = task with
            {
                Flow = flow with { Kind = "unknown", Implementation = "unknown" },
            },
        };

        Assert.Equal(FlowErrorCodes.RecordFormat, Mapper.FromDocument(changed).FirstError.Code);
    }

    [Fact]
    public void Several_codecs_for_the_same_identity_are_rejected()
    {
        TaskRecordSerializationMapper mapper = new([new FakeCodec(), new FakeCodec()]);

        Assert.Equal(FlowErrorCodes.RecordWrite, mapper.ToDocument(Record()).FirstError.Code);
    }

    private static TaskRecord RoundTrip(TaskRecord record) =>
        Mapper.FromDocument(Mapper.ToDocument(record).ThrowIfError()).ThrowIfError();

    private static TaskRecordDocument Document() => Mapper.ToDocument(Record()).ThrowIfError();

    private static TaskRecord Record() =>
        TaskRecordFixture.Create() with
        {
            Flow = new FlowRecord(
                new FlowIdentity(new FlowName("默认"), "fake", "fake"),
                new FakeDefinition("定义"),
                new FakeState("状态")),
        };

    private sealed record FakeDefinition(string Value) : IFlowDefinitionRecord;

    private sealed record FakeState(string Value) : IFlowStateRecord;

    private sealed class FakeCodec : IFlowRecordCodec
    {
        public bool Handles(FlowIdentity identity) => identity.Kind == "fake";

        public IFlowDefinitionRecord ReadDefinition(FlowRecordData data) =>
            new FakeDefinition(data.Definition.GetProperty("Value").GetString() ?? string.Empty);

        public IFlowStateRecord ReadState(FlowRecordData data) =>
            new FakeState(data.State.GetProperty("Value").GetString() ?? string.Empty);

        public FlowRecordData Write(IFlowDefinitionRecord definition, IFlowStateRecord state) =>
            definition is FakeDefinition fakeDefinition && state is FakeState fakeState
                ? new FlowRecordData(
                    JsonSerializer.SerializeToElement(new { fakeDefinition.Value }),
                    JsonSerializer.SerializeToElement(new { fakeState.Value }))
                : throw new NotSupportedException();
    }
}