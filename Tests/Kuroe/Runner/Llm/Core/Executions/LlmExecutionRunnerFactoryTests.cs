using System.Text.Json;
using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Execution.Shared;
using Kuroe.Tool;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Runner.Llm.Tests.Executions;

/// <summary>Llm 恢复准备：无法完成的执行不允许恢复，负载格式与模型在准备阶段校验。</summary>
public sealed class LlmExecutionRunnerFactoryTests
{
    [Fact]
    public void Prepare_rejects_an_execution_that_reported_cannot_complete()
    {
        LlmExecutionRunnerFactory factory = new(new ExecutionKind("Llm"), Factory(), new StatusReader());
        ExecutionRestoreContext context = Context(new ExecutionContract(CannotCompleteReason: "缺少资料"));

        Assert.True(factory.Prepare(context).IsError);
    }

    [Fact]
    public void Prepare_accepts_a_readable_session_payload()
    {
        LlmExecutionRunnerFactory factory = new(new ExecutionKind("Llm"), Factory(), new StatusReader());
        ExecutionRestoreContext context = Context(ExecutionContract.Empty);

        Assert.False(factory.Prepare(context).IsError);
    }

    [Fact]
    public void Prepare_rejects_a_payload_from_another_model()
    {
        LlmExecutionRunnerFactory factory = new(new ExecutionKind("Llm"), Factory(), new StatusReader());
        ExecutionRestoreContext context = Context(ExecutionContract.Empty, model: "other");

        Assert.True(factory.Prepare(context).IsError);
    }

    [Fact]
    public void Prepare_rejects_an_unknown_payload_format()
    {
        LlmExecutionRunnerFactory factory = new(new ExecutionKind("Llm"), Factory(), new StatusReader());
        ExecutionRestoreContext context = Context(ExecutionContract.Empty, format: "llm.session.v2");

        Assert.True(factory.Prepare(context).IsError);
    }

    private static SessionFactory Factory() =>
        new(new ModelAccess(), new ToolFactory(), new ToolJson());

    private static ExecutionRestoreContext Context(
        ExecutionContract contract,
        string model = "fake",
        string format = AgentSessionCodec.Format)
    {
        JsonElement empty = JsonSerializer.SerializeToElement(new { });
        ExecutionRestoreState payload = format == AgentSessionCodec.Format
            ? AgentSessionCodec.Encode(new LlmAgentSessionPayload(model, empty))
            : new ExecutionRestoreState(format, "{}");

        ExecutionRequest request = new(
            new ExecutionOwnerId("1"),
            new ExecutionTargetId("实施"),
            new ExecutionKind("Llm"),
            1,
            [],
            [],
            new Dictionary<ExecutionPortName, string>(),
            []);
        IExecutionTarget target = new FakeTarget(
            new LlmExecutionParameters(null, [], [], new Dictionary<string, IReadOnlyDictionary<string, string>>(), "fake"));

        return new ExecutionRestoreContext(
            new ExecutionId(1),
            request,
            target,
            payload,
            contract);
    }

    private sealed class FakeTarget(IExecutionParameters parameters) : IExecutionTarget
    {
        public ExecutionTargetId Id { get; } = new("实施");

        public ExecutionKind Kind => new("Llm");


        public IExecutionParameters Parameters { get; } = parameters;
    }

    private sealed class ModelAccess : IModelAccess
    {
        public ErrorOr<ModelConnection> Connect(ModelName model) =>
            Error.Validation("Model.Missing", "模型不存在。");

        public ErrorOr<Success> Check(ModelName model) => Result.Success;
    }

    private sealed class ToolFactory : IToolFactory
    {
        public IReadOnlyList<ITool> Create(
            ExecutionId execution,
            ExecutionRequest request,
            LlmExecutionParameters parameters) => [];
    }

    private sealed class ToolJson : IToolJson
    {
        public JsonElement Schema(IReadOnlyList<ToolParameter> parameters) =>
            JsonSerializer.SerializeToElement(new { });

        public string Describe(AIFunctionArguments arguments) => string.Empty;
    }

    private sealed class StatusReader : IExecutionStatusReader
    {
        public ErrorOr<ExecutionStatus> Read(ExecutionId id) =>
            Error.Failure("Execution.Missing", "执行不存在。");
    }
}
