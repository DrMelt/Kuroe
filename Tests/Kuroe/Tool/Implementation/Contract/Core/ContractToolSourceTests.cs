using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
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

    private sealed class FakePorts : IPortSubmitter
    {
        public ErrorOr<ExecutionStatus> SubmitValues(
            ExecutionId id,
            IReadOnlyDictionary<string, PortValue> values) =>
            new ExecutionStatus(NodePath.Of("节点"), 1, 1, NodeState.Running, [], null);
    }

    private sealed class FakeCannotComplete : ICannotCompleteSubmitter
    {
        public ErrorOr<ExecutionStatus> Report(ExecutionId id, string? reason) =>
            new ExecutionStatus(NodePath.Of("节点"), 1, 1, NodeState.Running, [], null);
    }

    private sealed class FakeStatus : IExecutionStatusReader
    {
        public ErrorOr<ExecutionStatus> Read(ExecutionId id) =>
            new ExecutionStatus(NodePath.Of("节点"), 1, 1, NodeState.Running, [], null);
    }
}
