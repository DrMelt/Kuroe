using Kuroe.Flow.Persistence.Serialization.Shared;
using Kuroe.Flow.Shared;
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
    public void Read_accepts_previous_version()
    {
        TaskRecordCodec codec = new();
        string json = codec.Write(TaskRecordFixture.Create()).ThrowIfError();
        string legacy = json.Replace(
            $"\"Version\": {TaskRecordSerializationVersion.Current}",
            $"\"Version\": {TaskRecordSerializationVersion.MinimumReadable}",
            StringComparison.Ordinal);
        Assert.Contains($"\"Version\": {TaskRecordSerializationVersion.MinimumReadable}", legacy);

        Assert.False(codec.Read(legacy).IsError);
    }

    [Fact]
    public void Read_rejects_unsupported_old_version_before_mapping_fields()
    {
        const string legacy = """{"Version":1,"Task":{"Nodes":[{"Kind":"Executable","PublishedExecution":1}]}}""";

        Assert.Equal(FlowErrorCodes.RecordVersion, new TaskRecordCodec().Read(legacy).FirstError.Code);
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
    public void Write_keeps_non_ascii_characters()
    {
        string json = new TaskRecordCodec().Write(TaskRecordFixture.Create()).ThrowIfError();

        Assert.Contains("计划结果", json);
    }
}
