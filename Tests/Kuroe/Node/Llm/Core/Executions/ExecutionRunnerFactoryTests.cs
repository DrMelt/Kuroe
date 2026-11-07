using ApiHub.Shared.Models;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Sessions;
using Xunit;

namespace Kuroe.Node.Llm.Tests.Executions;

/// <summary>Llm 恢复准备：无法完成的执行不允许恢复，其余可恢复状态由会话历史决定。</summary>
public sealed class ExecutionRunnerFactoryTests
{
    [Fact]
    public void Prepare_rejects_an_execution_that_reported_cannot_complete()
    {
        ExecutionRunnerFactory factory = new(null!, null!);
        RestorePreparationContext context = Context(new ExecutionContract(CannotCompleteReason: "缺少资料"));

        Assert.True(factory.Prepare(context).IsError);
    }

    [Fact]
    public void Prepare_accepts_a_restorable_session_without_a_cannot_complete_report()
    {
        ExecutionRunnerFactory factory = new(null!, null!);
        RestorePreparationContext context = Context(ExecutionContract.Empty);

        Assert.False(factory.Prepare(context).IsError);
    }

    private static RestorePreparationContext Context(ExecutionContract contract)
    {
        LlmExecutionState state = new(
            "fake",
            [new LlmMessageState("user", [new LlmContentState("text", Text: "继续")])]);

        return new RestorePreparationContext(
            new ExecutionInput
            {
                Task = new TaskId(1),
                Node = NodePath.Of("实施"),
                Type = null!,
                Declaration = new KuroeNodeDeclaration(ModelName.Create("fake").Value),
            },
            LlmExecutionStateCodec.Encode(state),
            contract);
    }
}