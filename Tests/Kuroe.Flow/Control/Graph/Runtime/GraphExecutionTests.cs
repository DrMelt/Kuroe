using Kuroe.Flow.Control.Graph.Runtime;
using Kuroe.Execution.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Control.Graph.Tests.Runtime;

/// <summary>执行的过程记录与过程事件：附加读取视图、播发、进度与收口后停止。</summary>
public sealed class GraphExecutionTests
{
    /// <summary>测试用的执行核，派发器不启动后台任务。</summary>
    private static Kuroe.Execution.ExecutionRuntime Runtime() =>
        new(
            new Kuroe.Execution.ExecutionDispatcher(),
            new Kuroe.Execution.ExecutionRunnerRegistry(),
            new Kuroe.Execution.ExecutionRestorerRegistry());

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
                new Kuroe.Flow.Node.Shared.StaticGraph.Ports.Port(
                    new PortName("结论"),
                    Kuroe.Flow.Node.Shared.StaticGraph.Ports.PortTypes.Text,
                    description: "结论文本"),
            ],
        };

        Kuroe.Execution.Shared.ExecutionRequest request = GraphExecution.BuildRequest(input);
        Kuroe.Execution.Shared.ExecutionOutputPort port = Assert.Single(request.OutputPorts);

        Assert.Equal("结论", port.Name.Value);
        Assert.True(port.Required);
        Assert.Equal("结论文本", port.Description);
    }

    [Fact]
    public void Request_carries_the_execution_limit()
    {
        Kuroe.Execution.Shared.ExecutionRequest request = GraphExecution.BuildRequest(Input() with { MaxAttempts = 7 });

        Assert.Equal(7, request.MaxAttempts);
    }

    private static GraphExecution Create()
    {
        GraphExecutionInput input = Input();
        Kuroe.Execution.Shared.ExecutionRequest request = GraphExecution.BuildRequest(input);
        Kuroe.Execution.Shared.IExecution unit = Runtime().Create(
            new Kuroe.Execution.Shared.ExecutionId(1),
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