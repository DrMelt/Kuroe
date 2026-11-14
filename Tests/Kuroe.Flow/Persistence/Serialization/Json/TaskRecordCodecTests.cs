using Kuroe.Execution.Records.Shared;
using Kuroe.Flow.Graph.Records.Shared;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Flow.Persistence.Serialization.Shared;
using Kuroe.Tasks.Shared;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.Json.Tests;

/// <summary>JSON 存档编解码的契约实现。</summary>
public sealed class TaskRecordCodecTests : TaskRecordCodecContractTests
{
    protected override ITaskRecordCodec CreateCodec() => TaskRecordCodecs.Codec();

    [Fact]
    public void Write_emits_current_version()
    {
        string json = TaskRecordCodecs.Codec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains($"\"Version\": {TaskRecordSerializationVersion.Current}", json);
    }

    [Fact]
    public void Read_rejects_malformed_json()
    {
        Assert.True(TaskRecordCodecs.Codec().Read("{").IsError);
    }

    [Fact]
    public void Read_rejects_unknown_version()
    {
        const string future = """{"Version":999,"Task":{}}""";

        Assert.Equal(FlowErrorCodes.RecordVersion, TaskRecordCodecs.Codec().Read(future).FirstError.Code);
    }

    [Fact]
    public void Read_accepts_current_version()
    {
        TaskRecordCodec codec = TaskRecordCodecs.Codec();
        string json = codec.Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains($"\"Version\": {TaskRecordSerializationVersion.Current}", json);
        Assert.False(codec.Read(json).IsError);
    }

    [Fact]
    public void Read_rejects_unsupported_old_version_before_mapping_fields()
    {
        const string legacy = """{"Version":1,"Task":{"Nodes":[{"Kind":"Executable","PublishedExecution":1}]}}""";

        Assert.Equal(FlowErrorCodes.RecordVersion, TaskRecordCodecs.Codec().Read(legacy).FirstError.Code);
    }

    [Fact]
    public void Read_migrates_a_version_13_graph_archive()
    {
        TaskRecord record = TaskRecordCodecs.Codec().Read(Fixture("v13-graph.json")).ThrowIfError();

        Assert.Equal(new TaskId(TaskRecordFixture.DefaultTaskId), record.Identity.Id);
        GraphFlowStateRecord state = Assert.IsType<GraphFlowStateRecord>(record.Flow.State);
        Assert.Equal(5, state.Nodes.Count);
        Assert.Equal(2, state.Ports.Count);
        Assert.Equal(2, state.Pending.Steps.Count);
        Assert.IsType<GraphFlowDefinitionRecord>(record.Flow.Definition);
    }

    [Fact]
    public void Read_migrates_a_version_13_code_archive()
    {
        TaskRecord record = TaskRecordCodecs.Codec().Read(Fixture("v13-code.json")).ThrowIfError();

        CodeFlowDefinitionRecord definition = Assert.IsType<CodeFlowDefinitionRecord>(record.Flow.Definition);
        CodeFlowStateRecord state = Assert.IsType<CodeFlowStateRecord>(record.Flow.State);
        Assert.Equal("dialogue", definition.Implementation);
        Assert.Equal("中文", state.Values["language"]);
    }

    [Fact]
    public void Write_emits_port_history_without_inline_publication()
    {
        string json = TaskRecordCodecs.Codec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("\"Ports\":", json);
        Assert.DoesNotContain("\"Publication\":", json);
    }

    [Fact]
    public void Write_keeps_archive_field_name_for_rerun_targets()
    {
        string json = TaskRecordCodecs.Codec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("\"ReworkTargets\":", json);
        Assert.DoesNotContain("\"RerunTargets\":", json);
    }

    [Fact]
    public void Write_emits_save_time_and_block_fields()
    {
        string json = TaskRecordCodecs.Codec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("\"SavedAt\":", json);
        Assert.Contains("\"Block\":", json);
        Assert.Contains("\"Summary\": \"读取失败。\"", json);
    }

    [Fact]
    public void Write_emits_context_tokens()
    {
        string json = TaskRecordCodecs.Codec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("\"ContextTokens\": 90", json);
    }

    [Fact]
    public void Write_and_read_preserve_restore_data()
    {
        TaskRecord record = TaskRecordFixture.Create();
        ExecutionRecord first = record.Executions[0] with
        {
            RestoreData = new ExecutionRestoreRecord("test", """{"payload":true}"""),
            ContinuedFrom = new Kuroe.Execution.Shared.ExecutionId(6),
        };
        TaskRecord changed = record with { Executions = [first, .. record.Executions.Skip(1)] };
        TaskRecordCodec codec = TaskRecordCodecs.Codec();

        string json = codec.Write(changed).ThrowIfError();
        TaskRecord loaded = codec.Read(json).ThrowIfError();

        Assert.Contains("\"RestoreData\":", json);
        Assert.Equal(first.RestoreData, loaded.Executions[0].RestoreData);
        Assert.Equal(first.ContinuedFrom, loaded.Executions[0].ContinuedFrom);
    }

    [Fact]
    public void Write_and_read_preserve_execution_contract()
    {
        TaskRecord record = TaskRecordFixture.Create();
        Kuroe.Execution.Shared.ExecutionPortName port = new("结论");
        Kuroe.Execution.Shared.ExecutionOutputValue value = new(port, "内容", false);
        ExecutionContractRecord contract = new(
            new Dictionary<Kuroe.Execution.Shared.ExecutionPortName, Kuroe.Execution.Shared.ExecutionOutputValue> { [port] = value },
            "缺少资料");
        ExecutionRecord first = record.Executions[0] with { Contract = contract };
        TaskRecord changed = record with { Executions = [first, .. record.Executions.Skip(1)] };
        TaskRecordCodec codec = TaskRecordCodecs.Codec();

        string json = codec.Write(changed).ThrowIfError();
        TaskRecord loaded = codec.Read(json).ThrowIfError();
        ExecutionContractRecord? restored = loaded.Executions[0].Contract;

        Assert.Contains("\"Contract\":", json);
        Assert.NotNull(restored);
        Assert.Equal("缺少资料", restored.CannotCompleteReason);
        Assert.Equal(value, Assert.Single(restored.Outputs!).Value);
    }

    [Fact]
    public void Write_keeps_non_ascii_characters()
    {
        string json = TaskRecordCodecs.Codec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("计划结果", json);
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}