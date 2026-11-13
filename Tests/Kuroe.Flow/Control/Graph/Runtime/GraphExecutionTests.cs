using ErrorOr;
using Kuroe.Flow.Control.Graph;
using Kuroe.Flow.Control.Graph.Engine;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Xunit;
using IExecutionRestoreDataSource = Kuroe.Flow.Execution.IExecutionRestoreDataSource;
using IExecutionUsageSource = Kuroe.Flow.Execution.IExecutionUsageSource;
using ExecutionRestoreState = Kuroe.Flow.Execution.ExecutionRestoreState;
using ExecutionUsage = Kuroe.Flow.Execution.ExecutionUsage;
using NeutralExecutionOutput = Kuroe.Flow.Execution.ExecutionOutput;
using TokenUsage = Kuroe.Flow.Execution.TokenUsage;

namespace Kuroe.Flow.Control.Graph.Tests;

/// <summary>执行的过程记录与过程事件：附加读取视图、播发、进度与收口后停止。</summary>
public sealed class GraphExecutionTests
{
    /// <summary>测试用的执行核，派发器不启动后台任务。</summary>
    private static Kuroe.Flow.Execution.IExecutionRuntime Runtime() =>
        new Kuroe.Flow.Execution.ExecutionRuntime(
            new Kuroe.Flow.Execution.ExecutionDispatcher(),
            new Kuroe.Flow.Execution.ExecutionRunnerRegistry(),
            new Kuroe.Flow.Execution.ExecutionRestorerRegistry());

    [Fact]
    public void Snapshot_without_journal_has_no_entries()
    {
        GraphExecution execution = Create();

        GraphExecutionSnapshot snapshot = execution.Snapshot();

        Assert.Equal(ExecutionState.Queued, snapshot.State);
        Assert.Empty(snapshot.Journal);
        Assert.False(snapshot.HasRestoreData);
    }

    [Fact]
    public void Request_carries_required_output_port_and_description()
    {
        GraphExecutionInput input = Input() with
        {
            OutputPorts =
            [
                new Kuroe.Flow.Node.StaticGraph.Ports.Port(
                    new PortName("结论"),
                    Kuroe.Flow.Node.StaticGraph.Ports.PortTypes.Text,
                    description: "结论文本"),
            ],
        };

        Kuroe.Flow.Execution.ExecutionRequest request = GraphExecution.BuildRequest(input);
        Kuroe.Flow.Execution.ExecutionOutputPort port = Assert.Single(request.OutputPorts);

        Assert.Equal("结论", port.Name.Value);
        Assert.True(port.Required);
        Assert.Equal("结论文本", port.Description);
    }

    private static GraphExecution Create()
    {
        GraphExecutionInput input = Input();
        Kuroe.Flow.Execution.ExecutionRequest request = GraphExecution.BuildRequest(input);
        Kuroe.Flow.Execution.IExecution unit = Runtime().Create(
            new Kuroe.Flow.Execution.ExecutionId(1),
            request);

        return new GraphExecution(input, unit, 0);
    }

    private static GraphExecutionInput Input() => new()
    {
        Task = new TaskId(1),
        Node = NodePath.Of("实施"),
        Type = TestNodeTypes.Executable,
    };
}