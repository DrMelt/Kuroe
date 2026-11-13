using Kuroe.Flow.Runtime;
using RuntimeExecution = Kuroe.Flow.Control.Graph.GraphExecution;
using Kuroe.Flow.Control.Graph.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Ports;
using Kuroe.Flow.Node.StaticGraph.Nodes;
using Kuroe.Flow.Node.StaticGraph.Ports;
using Xunit;
using ExecutableNode = Kuroe.Flow.Node.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Tests.Runtime.Nodes;

/// <summary>执行节点的发布状态：直接构造节点、执行与端口值，断言触发位与缺口，不启动任务。</summary>
public sealed class RuntimeExecutableTests
{
    /// <summary>测试用的执行核，派发器不启动后台任务。</summary>
    private static Kuroe.Flow.Execution.IExecutionRuntime Runtime() =>
        new Kuroe.Flow.Execution.ExecutionRuntime(
            new Kuroe.Flow.Execution.ExecutionDispatcher(),
            new Kuroe.Flow.Execution.ExecutionRunnerRegistry(),
            new Kuroe.Flow.Execution.ExecutionRestorerRegistry());
    private static readonly PortName Conclusion = new("结论");

    [Fact]
    public void Published_false_trigger_silences_the_port()
    {
        (RuntimeExecutable node, RuntimeExecution execution, Kuroe.Flow.Execution.IExecutionContractWriter contracts) = Node();
        Submit(contracts, execution, Values(new PortValue("内容", false)));

        Assert.True(node.Triggers(Conclusion));

        node.Publish(execution);

        Assert.False(node.Triggers(Conclusion));
    }

    [Fact]
    public void Published_true_trigger_fires_the_port()
    {
        (RuntimeExecutable node, RuntimeExecution execution, Kuroe.Flow.Execution.IExecutionContractWriter contracts) = Node();
        Submit(contracts, execution, Values(new PortValue("内容", true)));
        node.Publish(execution);

        Assert.True(node.Triggers(Conclusion));
    }

    [Fact]
    public void Missing_submission_is_reported_as_outstanding_ports()
    {
        (RuntimeExecutable node, RuntimeExecution execution, Kuroe.Flow.Execution.IExecutionContractWriter contracts) = Node();

        Assert.Equal([Conclusion], node.OutstandingPorts(execution));

        Submit(contracts, execution, Values(new PortValue("内容")));

        Assert.Empty(node.OutstandingPorts(execution));
    }

    [Fact]
    public void Invalidate_removes_the_published_trigger_value()
    {
        (RuntimeExecutable node, RuntimeExecution execution, Kuroe.Flow.Execution.IExecutionContractWriter contracts) = Node();
        Submit(contracts, execution, Values(new PortValue("内容", false)));
        node.Publish(execution);
        Assert.False(node.Triggers(Conclusion));

        node.Invalidate();

        Assert.True(node.Triggers(Conclusion));
    }

    private static (RuntimeExecutable Node, RuntimeExecution Exec, Kuroe.Flow.Execution.IExecutionContractWriter Contracts) Node()
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
        GraphExecutionInput input = new()
        {
            Task = new TaskId(1),
            Node = executable.Path,
            Type = executable.Type,
            OutputPorts = executable.Outputs,
        };
        Kuroe.Flow.Execution.IExecutionRuntime runtime = Runtime();
        Kuroe.Flow.Execution.IExecution unit = runtime.Create(
            new Kuroe.Flow.Execution.ExecutionId(1),
            RuntimeExecution.BuildRequest(input));
        RuntimeExecution execution = new(input, unit, 0);

        return (new RuntimeExecutable(executable), execution, new Kuroe.Flow.Execution.ExecutionContractWriter());
    }

    private static void Submit(
        Kuroe.Flow.Execution.IExecutionContractWriter contracts,
        RuntimeExecution execution,
        Dictionary<PortName, PortValue> values) =>
        contracts.SubmitOutputs(execution.Unit, ToNeutralOutputs(values));

    private static Dictionary<PortName, PortValue> Values(PortValue value) =>
        new() { [Conclusion] = value };

    private static IReadOnlyDictionary<Kuroe.Flow.Execution.ExecutionPortName, Kuroe.Flow.Execution.ExecutionOutputValue>
        ToNeutralOutputs(IReadOnlyDictionary<PortName, PortValue> values) =>
        values.ToDictionary(
            pair => new Kuroe.Flow.Execution.ExecutionPortName(pair.Key.Value),
            pair => new Kuroe.Flow.Execution.ExecutionOutputValue(
                new Kuroe.Flow.Execution.ExecutionPortName(pair.Key.Value),
                pair.Value.Text,
                pair.Value.Fires));
}
