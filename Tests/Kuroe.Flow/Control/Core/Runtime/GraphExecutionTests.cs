using Kuroe.Executor.Shared.Ports;
using Kuroe.Flow.Control.Runtime;
using Kuroe.Flow.Graph.Shared.Model;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Tasks.Shared;
using Xunit;
using Kuroe.Execution.Shared.State;

namespace Kuroe.Flow.Control.Tests.Runtime;

/// <summary>执行的过程记录与过程事件：附加读取视图、播发、进度与收口后停止。</summary>
public sealed class GraphExecutionTests
{
    /// <summary>测试用的执行核，派发器不启动后台任务。</summary>
    private static Kuroe.Execution.ExecutionRuntime Runtime() =>
        new(new Kuroe.Execution.ExecutionDispatcher());

    [Fact(DisplayName = "没有过程记录的快照没有条目")]
    public void Snapshot_without_journal_has_no_entries()
    {
        GraphExecution execution = Create();

        GraphExecutionSnapshot snapshot = execution.Snapshot();

        Assert.Equal(ExecutionState.Queued, snapshot.State);
        Assert.Empty(snapshot.Journal);
        Assert.False(snapshot.HasRestoreData);
    }

    [Fact(DisplayName = "请求携带必填输出端口与说明")]
    public void Request_carries_required_output_port_and_description()
    {
        GraphExecutionInput input = Input() with
        {
            OutputPorts =
            [
                new Kuroe.Flow.Graph.Shared.StaticGraph.Ports.Port(
                    new PortName("结论"),
                    Kuroe.Executor.Shared.Ports.PortTypes.Text,
                    description: "结论文本"),
            ],
        };

        Kuroe.Execution.Shared.Request.ExecutionRequest request = GraphExecution.BuildRequest(input);
        Kuroe.Execution.Shared.Ports.ExecutionOutputPort port = Assert.Single(request.OutputPorts);

        Assert.Equal("结论", port.Name.Value);
        Assert.True(port.Required);
        Assert.Equal("结论文本", port.Description);
    }

    private static GraphExecution Create()
    {
        GraphExecutionInput input = Input();
        Kuroe.Execution.Shared.Request.ExecutionRequest request = GraphExecution.BuildRequest(input);
        Kuroe.Execution.Shared.State.IExecution unit = Runtime().Create(
            new Kuroe.Execution.Shared.Identity.ExecutionId(1),
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