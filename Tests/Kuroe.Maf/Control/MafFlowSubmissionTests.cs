using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Maf.Records.Shared;
using Kuroe.Maf.Persistence;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;
using System.Text.Json;
using Xunit;

namespace Kuroe.Maf.Control.Tests;

/// <summary>MAF 流程的提交与恢复：开始输入校验、默认值补齐与存档往返。</summary>
public sealed class MafFlowSubmissionTests
{
    [Fact]
    public void Prepare_requires_every_start_port_without_default()
    {
        ErrorOr<FlowSubmission> prepared = Submit([]);

        Assert.True(prepared.IsError);
        Assert.Contains(prepared.ErrorsOrEmptyList, error => error.Code == FlowErrorCodes.TaskInput);
    }

    [Fact]
    public void Prepare_rejects_unknown_start_ports()
    {
        ErrorOr<FlowSubmission> prepared = Submit([
            new StartInput(new PortName("目标"), "做点事"),
            new StartInput(MafFlowDefinition.ModelPort, "test-model"),
            new StartInput(new PortName("其他"), "多余"),
        ]);

        Assert.True(prepared.IsError);
    }

    [Fact]
    public void Prepare_fills_declared_defaults()
    {
        ErrorOr<FlowSubmission> prepared = Submit([
            new StartInput(MafFlowDefinition.ModelPort, "test-model"),
        ]);

        Assert.False(prepared.IsError);
        Assert.Equal("默认目标", prepared.Value.State.Inputs.Text(new PortName("目标")));
    }

    [Fact]
    public void Prepare_requires_a_model()
    {
        ErrorOr<FlowSubmission> prepared = Submit([
            new StartInput(new PortName("目标"), "做点事"),
        ]);

        Assert.True(prepared.IsError);
    }

    [Fact]
    public void Record_codec_round_trips_definition_and_state()
    {
        ErrorOr<FlowSubmission> prepared = Submit([
            new StartInput(new PortName("目标"), "做点事"),
            new StartInput(MafFlowDefinition.ModelPort, "test-model"),
        ]);
        Assert.False(prepared.IsError);

        FlowSubmission submission = prepared.Value;
        MafFlowStateRecord state = new(
            submission.State.Inputs,
            submission.StartTexts,
            "test-model",
            "session",
            "checkpoint",
            "{}",
            ["第二步"]);

        MafFlowRecordCodec codec = new();
        FlowRecordData data = codec.Write(submission.Definition!, state);
        IFlowDefinitionRecord definition = codec.ReadDefinition(data);
        IFlowStateRecord restored = codec.ReadState(data);

        Assert.Equal(MafFlowIdentity.Implementation, Assert.IsType<MafFlowDefinitionRecord>(definition).Implementation);
        MafFlowStateRecord roundTripped = Assert.IsType<MafFlowStateRecord>(restored);
        Assert.Equal("test-model", roundTripped.Model);
        Assert.Equal("session", roundTripped.CheckpointSession);
        Assert.Equal("checkpoint", roundTripped.CheckpointId);
        Assert.Equal(["第二步"], roundTripped.PendingSteps);
        Assert.Equal("做点事", roundTripped.Inputs.Text(new PortName("目标")));
    }

    [Fact]
    public void Prepare_rejects_duplicate_step_ids()
    {
        ErrorOr<FlowSubmission> prepared = Submit(
            Definition(
                [
                    new MafStep("重复", "甲", [new PortName("输出")], _ => new FakeTarget()),
                    new MafStep("重复", "乙", [new PortName("输出")], _ => new FakeTarget()),
                ],
                []),
            [new StartInput(MafFlowDefinition.ModelPort, "test-model")]);

        Assert.True(prepared.IsError);
    }

    [Fact]
    public void Prepare_rejects_connections_to_missing_steps()
    {
        ErrorOr<FlowSubmission> prepared = Submit(
            Definition(
                [new MafStep("唯一", "唯一", [new PortName("输出")], _ => new FakeTarget())],
                [new MafEdge("唯一", "不存在")]),
            [new StartInput(MafFlowDefinition.ModelPort, "test-model")]);

        Assert.True(prepared.IsError);
    }

    [Fact]
    public void Prepare_rejects_multiple_upstreams()
    {
        ErrorOr<FlowSubmission> prepared = Submit(
            Definition(
                [
                    new MafStep("左", "左", [new PortName("输出")], _ => new FakeTarget()),
                    new MafStep("右", "右", [new PortName("输出")], _ => new FakeTarget()),
                    new MafStep("汇合", "汇合", [new PortName("输出")], _ => new FakeTarget()),
                ],
                [new MafEdge("左", "汇合"), new MafEdge("右", "汇合")]),
            [new StartInput(MafFlowDefinition.ModelPort, "test-model")]);

        Assert.True(prepared.IsError);
    }

    [Fact]
    public void Prepare_rejects_definitions_without_a_model_port()
    {
        ErrorOr<FlowSubmission> prepared = Submit(
            new MafFlowDefinition(
                new FlowName("测试"),
                [new MafStartPort(new PortName("目标"), "默认目标")],
                [new MafStep("唯一", "唯一", [new PortName("输出")], _ => new FakeTarget())],
                []),
            [new StartInput(new PortName("目标"), "做点事")]);

        Assert.True(prepared.IsError);
    }

    [Fact]
    public void Prepare_rejects_steps_unreachable_from_the_entry()
    {
        ErrorOr<FlowSubmission> prepared = Submit(
            Definition(
                [
                    new MafStep("入口", "入口", [new PortName("输出")], _ => new FakeTarget()),
                    new MafStep("甲", "甲", [new PortName("输出")], _ => new FakeTarget()),
                    new MafStep("乙", "乙", [new PortName("输出")], _ => new FakeTarget()),
                ],
                [new MafEdge("甲", "乙"), new MafEdge("乙", "甲")]),
            [new StartInput(MafFlowDefinition.ModelPort, "test-model")]);

        Assert.True(prepared.IsError);
        Assert.Contains(prepared.ErrorsOrEmptyList, error => error.Description.Contains("不可达", StringComparison.Ordinal));
    }

    [Fact]
    public void Record_codec_rejects_invalid_segments()
    {
        MafFlowRecordCodec codec = new();
        using JsonDocument document = JsonDocument.Parse("{}");
        FlowRecordData empty = new(document.RootElement, document.RootElement);

        Assert.Throws<FormatException>(() => codec.ReadDefinition(empty));
        Assert.Throws<FormatException>(() => codec.ReadState(empty));
    }

    [Fact]
    public void Record_codec_handles_only_maf_flow_kind()
    {
        MafFlowRecordCodec codec = new();

        Assert.True(codec.Handles(new FlowIdentity(new FlowName("测试"), MafFlowKind.Value, MafFlowIdentity.Implementation)));
        Assert.False(codec.Handles(new FlowIdentity(new FlowName("测试"), "graph", "graph")));
    }

    private static ErrorOr<FlowSubmission> Submit(StartInput[] values) => Submit(Definition(), values);

    private static ErrorOr<FlowSubmission> Submit(MafFlowDefinition definition, StartInput[] values) =>
        new MafFlowSubmission(new MafFlowCatalog([definition]), new UnusedGateway())
            .Prepare(new TaskId(1), definition.Name, new StartInputs(values), "标题");

    private static MafFlowDefinition Definition(
        IReadOnlyList<MafStep> steps,
        IReadOnlyList<MafEdge> edges) => new(
        new FlowName("测试"),
        [
            new MafStartPort(new PortName("目标"), "默认目标"),
            new MafStartPort(MafFlowDefinition.ModelPort),
        ],
        steps,
        edges);

    private static MafFlowDefinition Definition() => Definition(
        [
            new MafStep("第一步", "第一步", [new PortName("输出")], _ => new FakeTarget()),
            new MafStep("第二步", "第二步", [new PortName("输出")], _ => new FakeTarget()),
        ],
        [new MafEdge("第一步", "第二步")]);

    private sealed class FakeTarget : IExecutionTarget
    {
        public ExecutionTargetId Id => new("第一步");

        public string Kind => "test";

        public bool UsesDefaultRunner => false;

        public IExecutionParameters Parameters => EmptyExecutionParameters.Instance;
    }
}