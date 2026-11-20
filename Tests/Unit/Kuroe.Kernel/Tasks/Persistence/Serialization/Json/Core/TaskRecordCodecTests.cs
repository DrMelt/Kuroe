using Kuroe.Kernel.Execution.Records.Shared;
using Kuroe.Kernel.Tasks.Records.Shared;
using Kuroe.Kernel.Tasks.Persistence.Serialization.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Kernel.Tasks.Persistence.Serialization.Json.Tests;

/// <summary>JSON 存档编解码的契约实现。</summary>
public sealed class TaskRecordCodecTests : TaskRecordCodecContractTests
{
    protected override ITaskRecordCodec CreateCodec() => TaskRecordCodecs.Codec();

    [Fact(DisplayName = "写入输出当前版本")]
    public void Write_emits_current_version()
    {
        string json = TaskRecordCodecs.Codec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains($"\"Version\": {TaskRecordSerializationVersion.Current}", json);
    }

    [Fact(DisplayName = "读取拒绝格式错误的 JSON")]
    public void Read_rejects_malformed_json()
    {
        Assert.True(TaskRecordCodecs.Codec().Read("{").IsError);
    }

    [Fact(DisplayName = "读取拒绝未知版本")]
    public void Read_rejects_unknown_version()
    {
        const string future = """{"Version":999,"Task":{}}""";

        Assert.Equal(FlowErrorCodes.RecordVersion, TaskRecordCodecs.Codec().Read(future).FirstError.Code);
    }

    [Fact(DisplayName = "读取接受当前版本")]
    public void Read_accepts_current_version()
    {
        TaskRecordCodec codec = TaskRecordCodecs.Codec();
        string json = codec.Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains($"\"Version\": {TaskRecordSerializationVersion.Current}", json);
        Assert.False(codec.Read(json).IsError);
    }

    [Fact(DisplayName = "读取在映射字段前拒绝不支持的旧版本")]
    public void Read_rejects_unsupported_old_version_before_mapping_fields()
    {
        const string legacy = """{"Version":1,"Task":{"Nodes":[{"Kind":"Executable","PublishedExecution":1}]}}""";

        Assert.Equal(FlowErrorCodes.RecordVersion, TaskRecordCodecs.Codec().Read(legacy).FirstError.Code);
    }

    [Theory]
    [InlineData(13, Label = "版本十三")]
    [InlineData(14, Label = "版本十四")]
    [InlineData(15, Label = "版本十五")]
    public void Read_rejects_previous_versions(int version)
    {
        string previous = $"{{\"Version\":{version},\"Task\":{{}}}}";

        Assert.Equal(FlowErrorCodes.RecordVersion, TaskRecordCodecs.Codec().Read(previous).FirstError.Code);
    }

    [Fact(DisplayName = "写入输出端口历史且不内联发布")]
    public void Write_emits_port_history_without_inline_publication()
    {
        string json = TaskRecordCodecs.Codec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("\"Ports\":", json);
        Assert.DoesNotContain("\"Publication\":", json);
    }

    [Fact(DisplayName = "写入为重跑目标保留存档字段名")]
    public void Write_keeps_archive_field_name_for_rerun_targets()
    {
        string json = TaskRecordCodecs.Codec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("\"ReworkTargets\":", json);
        Assert.DoesNotContain("\"RerunTargets\":", json);
    }

    [Fact(DisplayName = "写入输出保存时间与阻塞字段")]
    public void Write_emits_save_time_and_block_fields()
    {
        string json = TaskRecordCodecs.Codec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("\"SavedAt\":", json);
        Assert.Contains("\"Block\":", json);
        Assert.Contains("\"Summary\": \"读取失败。\"", json);
    }

    [Fact(DisplayName = "读取接受旧版尝试字段")]
    public void Read_accepts_legacy_attempt_field()
    {
        TaskRecordCodec codec = TaskRecordCodecs.Codec();
        string json = codec.Write(TaskRecordFixture.Create()).ThrowIfError();

        System.Text.Json.Nodes.JsonObject root = System.Text.Json.Nodes.JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException("存档不是 JSON 对象。");
        System.Text.Json.Nodes.JsonArray executions = root["Task"]?["Executions"]?.AsArray()
            ?? throw new InvalidOperationException("存档没有执行列表。");
        System.Text.Json.Nodes.JsonObject execution = executions[0]?.AsObject()
            ?? throw new InvalidOperationException("存档没有执行。");
        execution["Attempt"] = 2;

        TaskRecord loaded = codec.Read(root.ToJsonString()).ThrowIfError();

        Assert.Equal(TaskRecordFixture.Create().Executions[0].Id, loaded.Executions[0].Id);
    }

    [Fact(DisplayName = "写入省略旧版尝试字段")]
    public void Write_omits_legacy_attempt_field()
    {
        string json = TaskRecordCodecs.Codec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.DoesNotContain("\"Attempt\"", json);
    }

    [Fact(DisplayName = "写入输出上下文用量")]
    public void Write_emits_context_tokens()
    {
        string json = TaskRecordCodecs.Codec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("\"ContextTokens\": 90", json);
    }

    [Fact(DisplayName = "写入与读取保留恢复数据")]
    public void Write_and_read_preserve_restore_data()
    {
        TaskRecord record = TaskRecordFixture.Create();
        ExecutionRecord first = record.Executions[0] with
        {
            RestoreData = new ExecutionRestoreRecord("test", """{"payload":true}"""),
            ContinuedFrom = new Kuroe.Kernel.Execution.Shared.Identity.ExecutionId(6),
        };
        TaskRecord changed = record with { Executions = [first, .. record.Executions.Skip(1)] };
        TaskRecordCodec codec = TaskRecordCodecs.Codec();

        string json = codec.Write(changed).ThrowIfError();
        TaskRecord loaded = codec.Read(json).ThrowIfError();

        Assert.Contains("\"RestoreData\":", json);
        Assert.Equal(first.RestoreData, loaded.Executions[0].RestoreData);
        Assert.Equal(first.ContinuedFrom, loaded.Executions[0].ContinuedFrom);
    }

    [Fact(DisplayName = "写入与读取保留执行契约")]
    public void Write_and_read_preserve_execution_contract()
    {
        TaskRecord record = TaskRecordFixture.Create();
        Kuroe.Kernel.Execution.Shared.Ports.ExecutionPortName port = new("结论");
        Kuroe.Kernel.Execution.Shared.Ports.ExecutionOutputValue value = new(port, "内容", false);
        ExecutionContractRecord contract = new(
            new Dictionary<Kuroe.Kernel.Execution.Shared.Ports.ExecutionPortName, Kuroe.Kernel.Execution.Shared.Ports.ExecutionOutputValue> { [port] = value },
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
        IReadOnlyDictionary<
            Kuroe.Kernel.Execution.Shared.Ports.ExecutionPortName,
            Kuroe.Kernel.Execution.Shared.Ports.ExecutionOutputValue> outputs =
            restored.Outputs ?? throw new InvalidOperationException("契约没有产出。");
        Assert.Equal(value, Assert.Single(outputs).Value);
    }

    [Fact(DisplayName = "写入保留非 ASCII 字符")]
    public void Write_keeps_non_ascii_characters()
    {
        string json = TaskRecordCodecs.Codec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("计划结果", json);
    }
}
