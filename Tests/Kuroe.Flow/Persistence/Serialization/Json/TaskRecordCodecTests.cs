using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Persistence.Serialization.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.Json.Tests;

/// <summary>JSON 存档编解码的契约实现。</summary>
public sealed class TaskRecordCodecTests : TaskRecordCodecContractTests
{
    protected override ITaskRecordCodec CreateCodec() => new TaskRecordCodec();

    [Fact]
    public void Write_emits_current_version()
    {
        string json = new TaskRecordCodec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains($"\"Version\": {TaskRecordSerializationVersion.Current}", json);
    }

    [Fact]
    public void Read_rejects_malformed_json()
    {
        Assert.True(new TaskRecordCodec().Read("{").IsError);
    }

    [Fact]
    public void Read_rejects_unknown_version()
    {
        const string future = """{"Version":999,"Task":{}}""";

        Assert.Equal(FlowErrorCodes.RecordVersion, new TaskRecordCodec().Read(future).FirstError.Code);
    }

    [Fact]
    public void Read_accepts_current_version()
    {
        TaskRecordCodec codec = new();
        string json = codec.Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains($"\"Version\": {TaskRecordSerializationVersion.Current}", json);
        Assert.False(codec.Read(json).IsError);
    }

    [Fact]
    public void Read_rejects_unsupported_old_version_before_mapping_fields()
    {
        const string legacy = """{"Version":1,"Task":{"Nodes":[{"Kind":"Executable","PublishedExecution":1}]}}""";

        Assert.Equal(FlowErrorCodes.RecordVersion, new TaskRecordCodec().Read(legacy).FirstError.Code);
    }

    [Fact]
    public void Write_emits_port_history_without_inline_publication()
    {
        string json = new TaskRecordCodec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("\"Ports\":", json);
        Assert.DoesNotContain("\"Publication\":", json);
    }

    [Fact]
    public void Write_keeps_archive_field_name_for_rerun_targets()
    {
        string json = new TaskRecordCodec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("\"ReworkTargets\":", json);
        Assert.DoesNotContain("\"RerunTargets\":", json);
    }

    [Fact]
    public void Write_emits_save_time_and_block_fields()
    {
        string json = new TaskRecordCodec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("\"SavedAt\":", json);
        Assert.Contains("\"Block\":", json);
        Assert.Contains("\"Summary\": \"读取失败。\"", json);
    }

    [Fact]
    public void Write_emits_context_tokens()
    {
        string json = new TaskRecordCodec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("\"ContextTokens\": 90", json);
    }

    [Fact]
    public void Write_and_read_preserve_restore_state()
    {
        TaskRecord record = TaskRecordFixture.Create();
        ExecutionRecord first = record.Executions[0] with
        {
            RestoreState = new ExecutionRestoreState("test", """{"payload":true}"""),
            ContinuedFrom = new ExecutionId(6),
        };
        TaskRecord changed = record with { Executions = [first, .. record.Executions.Skip(1)] };
        TaskRecordCodec codec = new();

        string json = codec.Write(changed).ThrowIfError();
        TaskRecord loaded = codec.Read(json).ThrowIfError();

        Assert.Contains("\"RestoreState\":", json);
        Assert.Equal(first.RestoreState, loaded.Executions[0].RestoreState);
        Assert.Equal(first.ContinuedFrom, loaded.Executions[0].ContinuedFrom);
    }

    [Fact]
    public void Write_and_read_preserve_execution_contract()
    {
        TaskRecord record = TaskRecordFixture.Create();
        PortName port = new("结论");
        PortValue value = new("内容", false);
        ExecutionContractRecord contract = new(
            new Dictionary<PortName, PortValue> { [port] = value },
            "缺少资料");
        ExecutionRecord first = record.Executions[0] with { Contract = contract };
        TaskRecord changed = record with { Executions = [first, .. record.Executions.Skip(1)] };
        TaskRecordCodec codec = new();

        string json = codec.Write(changed).ThrowIfError();
        TaskRecord loaded = codec.Read(json).ThrowIfError();
        ExecutionContractRecord? restored = loaded.Executions[0].Contract;

        Assert.Contains("\"Contract\":", json);
        Assert.NotNull(restored);
        Assert.Equal("缺少资料", restored.CannotCompleteReason);
        Assert.Equal(value, Assert.Single(restored.Ports!).Value);
    }

    [Fact]
    public void Write_keeps_non_ascii_characters()
    {
        string json = new TaskRecordCodec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("计划结果", json);
    }
}
