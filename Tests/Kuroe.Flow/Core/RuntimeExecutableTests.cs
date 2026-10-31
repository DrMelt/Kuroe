using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Tests;

/// <summary>执行节点的发布状态：直接构造节点、执行与端口值，断言触发位与缺口，不启动任务。</summary>
public sealed class RuntimeExecutableTests
{
    private static readonly PortName Conclusion = new("结论");

    [Fact]
    public void Published_false_trigger_silences_the_port()
    {
        (RuntimeExecutable node, Execution execution) = Node();
        node.CollectPorts(execution.Id, Values(new PortValue("内容", false)));

        Assert.True(node.Triggers(Conclusion));

        node.Publish(execution);

        Assert.False(node.Triggers(Conclusion));
    }

    [Fact]
    public void Published_true_trigger_fires_the_port()
    {
        (RuntimeExecutable node, Execution execution) = Node();
        node.CollectPorts(execution.Id, Values(new PortValue("内容", true)));
        node.Publish(execution);

        Assert.True(node.Triggers(Conclusion));
    }

    [Fact]
    public void Missing_submission_is_reported_as_a_shortfall()
    {
        (RuntimeExecutable node, Execution execution) = Node();

        Assert.Equal([Conclusion], node.Shortfall(execution.Id));

        node.CollectPorts(execution.Id, Values(new PortValue("内容")));

        Assert.Empty(node.Shortfall(execution.Id));
    }

    [Fact]
    public void Invalidate_removes_the_published_trigger_value()
    {
        (RuntimeExecutable node, Execution execution) = Node();
        node.CollectPorts(execution.Id, Values(new PortValue("内容", false)));
        node.Publish(execution);
        Assert.False(node.Triggers(Conclusion));

        node.Invalidate();

        Assert.True(node.Triggers(Conclusion));
    }

    private static (RuntimeExecutable Node, Execution Execution) Node()
    {
        ExecutableNode executable = new()
        {
            Path = NodePath.Of("节点"),
            Type = TestNodeTypes.Executable,
            Spec = new ExecutableSpec(),
            Inputs = [],
            Outputs = [new Port(Conclusion, PortTypes.Text, mode: PortMode.Trigger)],
            MaxExecutions = ExecutableNode.DefaultMaxExecutions,
        };
        Execution execution = new(new ExecutionId(1), new ExecutionInput
        {
            Task = new TaskId(1),
            Node = executable.Path,
            Type = executable.Type,
            OutputPorts = executable.Outputs,
        });

        return (new RuntimeExecutable(executable), execution);
    }

    private static Dictionary<PortName, PortValue> Values(PortValue value) =>
        new() { [Conclusion] = value };
}
