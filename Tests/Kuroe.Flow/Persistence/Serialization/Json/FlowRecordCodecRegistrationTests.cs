using Kuroe.Tasks.Shared;
using System.Text.Json;
using Kuroe.Flow.Persistence.Serialization.Shared;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.Json.Tests;

/// <summary>新增流程实现只需提供自己的记录与编解码器，不改中央序列化项目。</summary>
public sealed class FlowRecordCodecRegistrationTests
{
    [Fact]
    public void A_new_flow_round_trips_through_the_json_codec()
    {
        TaskRecord record = TaskRecordFixture.Create() with
        {
            Flow = new FlowRecord(
                new FlowIdentity(new FlowName("新流程"), "custom", "custom"),
                new CustomDefinition("定义值"),
                new CustomState("状态值")),
        };
        TaskRecordCodec codec = new(new TaskRecordSerializationMapper([new CustomCodec()]));

        string json = codec.Write(record).ThrowIfError();
        TaskRecord loaded = codec.Read(json).ThrowIfError();

        Assert.Contains("\"Definition\":", json);
        Assert.Contains("\"State\":", json);
        Assert.Equal(new CustomDefinition("定义值"), loaded.Flow.Definition);
        Assert.Equal(new CustomState("状态值"), loaded.Flow.State);
        Assert.Equal("custom", loaded.Flow.Identity.Implementation);
    }

    private sealed record CustomDefinition(string Value) : IFlowDefinitionRecord;

    private sealed record CustomState(string Value) : IFlowStateRecord;

    private sealed class CustomCodec : IFlowRecordCodec
    {
        public bool Handles(FlowIdentity identity) => identity.Kind == "custom";

        public IFlowDefinitionRecord ReadDefinition(FlowRecordData data) =>
            new CustomDefinition(data.Definition.GetProperty("Value").GetString() ?? string.Empty);

        public IFlowStateRecord ReadState(FlowRecordData data) =>
            new CustomState(data.State.GetProperty("Value").GetString() ?? string.Empty);

        public FlowRecordData Write(IFlowDefinitionRecord definition, IFlowStateRecord state) =>
            definition is CustomDefinition customDefinition && state is CustomState customState
                ? new FlowRecordData(
                    JsonSerializer.SerializeToElement(new { customDefinition.Value }),
                    JsonSerializer.SerializeToElement(new { customState.Value }))
                : throw new NotSupportedException();
    }
}