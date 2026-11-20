using System.Text.Json;
using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Tool;
using Microsoft.Extensions.AI;
using Xunit;
using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Kernel.Execution.Shared.Restore;

namespace Kuroe.Runner.Llm.Tests.Executions;

/// <summary>Llm 恢复准备：无法完成的执行不允许恢复，负载格式与模型在准备阶段校验。</summary>
public sealed class LlmExecutionRunnerFactoryTests
{
    [Fact(DisplayName = "准备拒绝报告无法完成的执行")]
    public void Prepare_rejects_an_execution_that_reported_cannot_complete()
    {
        LlmExecutionRunnerFactory factory = new(Factory());
        ExecutionRestoreContext context = Context(new ExecutionContract(CannotCompleteReason: "缺少资料"));

        Assert.True(factory.Prepare(context).IsError);
    }

    [Fact(DisplayName = "准备接受可读取的会话负载")]
    public void Prepare_accepts_a_readable_session_payload()
    {
        LlmExecutionRunnerFactory factory = new(Factory());
        ExecutionRestoreContext context = Context(ExecutionContract.Empty);

        Assert.False(factory.Prepare(context).IsError);
    }

    [Fact(DisplayName = "准备拒绝来自其他模型的负载")]
    public void Prepare_rejects_a_payload_from_another_model()
    {
        LlmExecutionRunnerFactory factory = new(Factory());
        ExecutionRestoreContext context = Context(ExecutionContract.Empty, model: "other");

        Assert.True(factory.Prepare(context).IsError);
    }

    [Fact(DisplayName = "准备拒绝未知负载格式")]
    public void Prepare_rejects_an_unknown_payload_format()
    {
        LlmExecutionRunnerFactory factory = new(Factory());
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
            new ExecutionStepId("实施"),
            [],
            [],
            new Dictionary<ExecutionPortName, string>(),
            []);
        ExecutionInvocation target = new(
            new ExecutionStepId("实施"),
            new LlmExecutionRunnerFactory(Factory()),
            new LlmExecutionParameters(null, [], [], new Dictionary<string, IReadOnlyDictionary<string, string>>(), "fake"));

        return new ExecutionRestoreContext(
            new ExecutionId(1),
            request,
            target,
            payload,
            contract);
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
            IExecutionRequestEntry requests,
            LlmExecutionParameters parameters) => [];
    }

    private sealed class ToolJson : IToolJson
    {
        public JsonElement Schema(IReadOnlyList<ToolParameter> parameters) =>
            JsonSerializer.SerializeToElement(new { });

        public string Describe(AIFunctionArguments arguments) => string.Empty;
    }

}
