using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.Shared.Tests;

/// <summary>执行检查点的数据形状：状态、历史、输入取值与出处。</summary>
public sealed class ExecutionCheckpointTests
{
    [Fact]
    public void Execution_checkpoint_carries_history_and_inputs()
    {
        ExecutionCheckpoint execution = TaskCheckpointFixture.Create().Executions[0];

        Assert.Equal(ExecutionState.Succeeded, execution.State);
        Assert.Equal(1, execution.ExecutionCount);
        Assert.Empty(execution.Failures);
        Assert.Single(execution.Journal);
        Assert.Single(execution.Inputs);
        Assert.Equal("目标内容", execution.Texts[new PortName("目标")]);
    }

    [Fact]
    public void Execution_checkpoint_preserves_context_source_kinds()
    {
        InputValueCheckpoint value = TaskCheckpointFixture.Create().Executions[0].Inputs[0];

        Assert.Equal(ContextSourceKind.Start, value.Origin.Kind);
        Assert.Null(value.Origin.Execution);
        Assert.Null(value.Origin.Node);
        Assert.Equal(new PortName("目标"), value.Origin.Port);
        Assert.Equal("目标内容", value.Text);
    }

    [Fact]
    public void Execution_checkpoint_keeps_interrupted_state()
    {
        ExecutionCheckpoint execution = TaskCheckpointFixture.Create().Executions[2];

        Assert.Equal(ExecutionState.Interrupted, execution.State);
        Assert.Contains("任务已暂停。", execution.Failures);
    }
}
