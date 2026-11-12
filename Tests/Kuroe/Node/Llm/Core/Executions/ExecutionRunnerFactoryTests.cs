using Kuroe.Flow.Execution;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Sessions;
using Xunit;

namespace Kuroe.Node.Llm.Tests.Executions;

/// <summary>Llm 恢复准备：无法完成的执行不允许恢复，其余可恢复数据由会话历史决定。</summary>
public sealed class ExecutionRunnerFactoryTests
{
    [Fact]
    public void Prepare_rejects_an_execution_that_reported_cannot_complete()
    {
        ExecutionRunnerFactory factory = new(null!, null!);
        ExecutionRestoreContext context = Context(new ExecutionContract(CannotCompleteReason: "缺少资料"));

        Assert.True(factory.Prepare(context).IsError);
    }

    [Fact]
    public void Prepare_accepts_a_restorable_session_without_a_cannot_complete_report()
    {
        ExecutionRunnerFactory factory = new(null!, null!);
        ExecutionRestoreContext context = Context(ExecutionContract.Empty);

        Assert.False(factory.Prepare(context).IsError);
    }

    private static ExecutionRestoreContext Context(ExecutionContract contract)
    {
        LlmExecutionState state = new(
            "fake",
            [new LlmMessageState("user", [new LlmContentState("text", Text: "继续")])]);

        ExecutionRequest request = new(
            new ExecutionOwnerId("1"),
            new ExecutionTargetId("实施"),
            "Llm",
            1,
            [],
            [],
            new Dictionary<ExecutionPortName, string>(),
            []);
        IExecutionTarget target = new FakeTarget(
            new LlmExecutionParameters(new LlmNodeType(null), "fake"));

        return new ExecutionRestoreContext(
            new ExecutionId(1),
            request,
            target,
            LlmExecutionStateCodec.Encode(state),
            contract);
    }

    private sealed class FakeTarget(IExecutionParameters parameters) : IExecutionTarget
    {
        public ExecutionTargetId Id { get; } = new("实施");

        public string Kind => "Llm";

        public bool UsesDefaultRunner => true;

        public IExecutionParameters Parameters { get; } = parameters;
    }
}