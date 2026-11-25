using Xunit;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Flow.Graph.Shared.Tests.RuntimeGraph.Execution;

/// <summary>执行状态的判定：终态与在跑。</summary>
public sealed class ExecutionStatesTests
{
    [Fact(DisplayName = "执行状态表明是否收口")]
    public void Execution_state_tells_whether_it_settles()
    {
        Assert.False(ExecutionState.Queued.IsSettled());
        Assert.True(ExecutionState.Queued.IsLive());

        Assert.False(ExecutionState.Running.IsSettled());
        Assert.True(ExecutionState.Running.IsLive());

        Assert.True(ExecutionState.Succeeded.IsSettled());
        Assert.False(ExecutionState.Succeeded.IsLive());

        Assert.True(ExecutionState.Failed.IsSettled());
        Assert.True(ExecutionState.Interrupted.IsSettled());
        Assert.False(ExecutionState.Interrupted.IsLive());
        Assert.Equal("已中断", ExecutionState.Interrupted.Label());
    }
}