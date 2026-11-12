using ErrorOr;
using Kuroe.Flow.Execution;
using Xunit;

namespace Kuroe.Tool.Contract.Tests;

/// <summary>契约工具源声明三条固定路径，运行时按执行创建全部载体。</summary>
public sealed class ContractToolSourceTests
{
    [Fact]
    public void Definitions_cover_the_three_contract_paths()
    {
        ContractToolSource source = new(new FakePorts(), new FakeCannotComplete(), new FakeStatus());

        Assert.Equal(
            [ContractTools.Port, ContractTools.CannotComplete, ContractTools.Status],
            source.Definitions.Select(definition => definition.Path.Value));
    }

    private sealed class FakePorts : IExecutionPortSubmitter
    {
        public ErrorOr<ExecutionStatus> Submit(
            ExecutionId id,
            IReadOnlyList<ExecutionOutputValue> values) =>
            Status();
    }

    private sealed class FakeCannotComplete : IExecutionCannotCompleteSubmitter
    {
        public ErrorOr<ExecutionStatus> Report(ExecutionId id, string reason) => Status();
    }

    private sealed class FakeStatus : IExecutionStatusReader
    {
        public ErrorOr<ExecutionStatus> Read(ExecutionId id) => Status();
    }

    private static ExecutionStatus Status() =>
        new(new ExecutionTargetId("节点"), 1, 1, ExecutionState.Running, [], null);
}
