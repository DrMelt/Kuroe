using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Maf.Definition;
using Kuroe.Maf.Records.Shared;
using Kuroe.Maf.Persistence;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;
using System.Text.Json;
using Xunit;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Maf.Control.Tests;

/// <summary>MAF 流程的提交与恢复：开始输入校验、默认值补齐与存档往返。</summary>
public sealed class MafFlowSubmissionTests
{
    private static IExecutionRunnerFactory TestRunner { get; } =
        new FakeRunnerFactory(() => new FakeRunner());

    [Theory]
    [InlineData("", Label = "空文本")]
    [InlineData("  ", Label = "空白文本")]
    [InlineData("甲/乙", Label = "含斜杠")]
    public void Step_id_rejects_invalid_text(string value)
    {
        Assert.True(MafStepId.Create(value).IsError);
    }

    [Fact(DisplayName = "步骤标识去掉两侧空白")]
    public void Step_id_trims_text()
    {
        Assert.Equal("甲", MafStepId.Create(" 甲 ").Value.Value);
    }

    [Fact(DisplayName = "准备要求每个没有默认值的开始端口")]
    public void Prepare_requires_every_start_port_without_default()
    {
        ErrorOr<FlowSubmission> prepared = Submit([]);

        Assert.True(prepared.IsError);
        Assert.Contains(prepared.ErrorsOrEmptyList, error => error.Code == FlowErrorCodes.TaskInput);
    }

    [Fact(DisplayName = "准备拒绝未知开始端口")]
    public void Prepare_rejects_unknown_start_ports()
    {
        ErrorOr<FlowSubmission> prepared = Submit([
            new StartInput(new PortName("目标"), "做点事"),
            new StartInput(MafFlowDefinition.ModelPort, "test-model"),
            new StartInput(new PortName("其他"), "多余"),
        ]);

        Assert.True(prepared.IsError);
    }

    [Fact(DisplayName = "准备填充声明的默认值")]
    public void Prepare_fills_declared_defaults()
    {
        ErrorOr<FlowSubmission> prepared = Submit([
            new StartInput(MafFlowDefinition.ModelPort, "test-model"),
        ]);

        Assert.False(prepared.IsError);
        Assert.Equal("默认目标", prepared.Value.State.Inputs.Text(new PortName("目标")));
    }

    [Fact(DisplayName = "准备要求模型")]
    public void Prepare_requires_a_model()
    {
        ErrorOr<FlowSubmission> prepared = Submit([
            new StartInput(new PortName("目标"), "做点事"),
        ]);

        Assert.True(prepared.IsError);
    }

    [Fact(DisplayName = "记录编解码往返定义与状态")]
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
        FlowRecordData data = codec.Write(submission.Definition!, state).Value;
        IFlowDefinitionRecord definition = codec.ReadDefinition(data).Value;
        IFlowStateRecord restored = codec.ReadState(data).Value;

        Assert.Equal(MafFlowIdentity.Implementation, Assert.IsType<MafFlowDefinitionRecord>(definition).Implementation);
        MafFlowStateRecord roundTripped = Assert.IsType<MafFlowStateRecord>(restored);
        Assert.Equal("test-model", roundTripped.Model);
        Assert.Equal("session", roundTripped.CheckpointSession);
        Assert.Equal("checkpoint", roundTripped.CheckpointId);
        Assert.Equal(["第二步"], roundTripped.PendingSteps);
        Assert.Equal("做点事", roundTripped.Inputs.Text(new PortName("目标")));
    }

    [Fact(DisplayName = "准备拒绝重复步骤标识")]
    public void Prepare_rejects_duplicate_step_ids()
    {
        ErrorOr<FlowSubmission> prepared = Submit(
            Definition(
                [
                    Step("重复", "甲"),
                    Step("重复", "乙"),
                ],
                []),
            [new StartInput(MafFlowDefinition.ModelPort, "test-model")]);

        Assert.True(prepared.IsError);
    }

    [Fact(DisplayName = "准备拒绝非法步骤标识")]
    public void Prepare_rejects_invalid_step_ids()
    {
        ErrorOr<FlowSubmission> prepared = Submit(
            Definition(
                [Step("甲/乙")],
                []),
            [new StartInput(MafFlowDefinition.ModelPort, "test-model")]);

        Assert.True(prepared.IsError);
    }

    [Fact(DisplayName = "准备拒绝目标工厂错误")]
    public void Prepare_rejects_target_factory_errors()
    {
        MafStep step = new(
            new MafStepId("步骤"),
            "步骤",
            [new PortName("输出")],
            new FailingInvocationFactory(),
            MafUpstreamInputs.Instance);
        ErrorOr<FlowSubmission> prepared = Submit(
            Definition([step], []),
            [new StartInput(MafFlowDefinition.ModelPort, "test-model")]);

        Assert.True(prepared.IsError);
    }

    [Fact(DisplayName = "准备拒绝目标标识不匹配")]
    public void Prepare_rejects_target_id_mismatch()
    {
        MafStep step = new(
            new MafStepId("步骤"),
            "步骤",
            [new PortName("输出")],
            new MismatchedInvocationFactory(),
            MafUpstreamInputs.Instance);
        ErrorOr<FlowSubmission> prepared = Submit(
            Definition([step], []),
            [new StartInput(MafFlowDefinition.ModelPort, "test-model")]);

        Assert.True(prepared.IsError);
        Assert.Contains(prepared.ErrorsOrEmptyList,
            error => error.Description.Contains("不一致", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "准备拒绝连接缺失步骤")]
    public void Prepare_rejects_connections_to_missing_steps()
    {
        ErrorOr<FlowSubmission> prepared = Submit(
            Definition(
                [Step("唯一")],
                [Edge("唯一", "不存在")]),
            [new StartInput(MafFlowDefinition.ModelPort, "test-model")]);

        Assert.True(prepared.IsError);
    }

    [Fact(DisplayName = "准备拒绝多个上游")]
    public void Prepare_rejects_multiple_upstreams()
    {
        ErrorOr<FlowSubmission> prepared = Submit(
            Definition(
                [
                    Step("左"),
                    Step("右"),
                    Step("汇合"),
                ],
                [Edge("左", "汇合"), Edge("右", "汇合")]),
            [new StartInput(MafFlowDefinition.ModelPort, "test-model")]);

        Assert.True(prepared.IsError);
    }

    [Fact(DisplayName = "准备拒绝没有模型端口的定义")]
    public void Prepare_rejects_definitions_without_a_model_port()
    {
        ErrorOr<FlowSubmission> prepared = Submit(
            new MafFlowDefinition(
                new FlowName("测试"),
                [new MafStartPort(new PortName("目标"), "默认目标")],
                [Step("唯一")],
                []),
            [new StartInput(new PortName("目标"), "做点事")]);

        Assert.True(prepared.IsError);
    }

    [Fact(DisplayName = "准备拒绝从入口不可达的步骤")]
    public void Prepare_rejects_steps_unreachable_from_the_entry()
    {
        ErrorOr<FlowSubmission> prepared = Submit(
            Definition(
                [
                    Step("入口"),
                    Step("甲"),
                    Step("乙"),
                ],
                [Edge("甲", "乙"), Edge("乙", "甲")]),
            [new StartInput(MafFlowDefinition.ModelPort, "test-model")]);

        Assert.True(prepared.IsError);
        Assert.Contains(prepared.ErrorsOrEmptyList, error => error.Description.Contains("不可达", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "记录编解码拒绝非法分段")]
    public void Record_codec_rejects_invalid_segments()
    {
        MafFlowRecordCodec codec = new();
        using JsonDocument document = JsonDocument.Parse("{}");
        FlowRecordData empty = new(document.RootElement, document.RootElement);

        Assert.True(codec.ReadDefinition(empty).IsError);
        Assert.True(codec.ReadState(empty).IsError);
    }

    [Fact(DisplayName = "记录编解码只处理 Maf 流程种类")]
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
            Step("第一步"),
            Step("第二步"),
        ],
        [Edge("第一步", "第二步")]);

    private static MafStep Step(string id, string? label = null) =>
        new(new MafStepId(id), label ?? id, [new PortName("输出")], Invocation(), MafUpstreamInputs.Instance);

    private static MafEdge Edge(string from, string to) =>
        new(new MafStepId(from), new MafStepId(to));

    private static MafStepInvocationFactory Invocation() =>
        new(TestRunner, _ => EmptyExecutionParameters.Instance);

    private sealed class FailingInvocationFactory : IMafStepInvocationFactory
    {
        public ErrorOr<ExecutionInvocation> Create(MafStepId step, string model, ExecutionParameterContext context) =>
            Error.Validation("Maf.Invocation", "没有执行装配。");
    }

    private sealed class MismatchedInvocationFactory : IMafStepInvocationFactory
    {
        public ErrorOr<ExecutionInvocation> Create(MafStepId step, string model, ExecutionParameterContext context) =>
            new ExecutionInvocation(
                new ExecutionStepId("其他"),
                TestRunner,
                EmptyExecutionParameters.Instance);
    }
}