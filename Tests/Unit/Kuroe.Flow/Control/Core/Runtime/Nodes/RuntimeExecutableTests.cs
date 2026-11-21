using ErrorOr;
using RuntimeExecution = Kuroe.Flow.Control.Runtime.GraphExecution;
using Kuroe.Flow.Control.Runtime.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Ports;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;
using ExecutableNode = Kuroe.Flow.Graph.Shared.StaticGraph.ExecutableNode;
using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Journal;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Flow.Control.Tests.Runtime.Nodes;

/// <summary>执行节点的发布状态：直接构造节点、执行与端口值，断言触发位与缺口，不启动任务。</summary>
public sealed class RuntimeExecutableTests
{
    private static readonly PortName Conclusion = new("结论");

    [Fact(DisplayName = "发布的 false 触发使端口静默")]
    public async Task Published_false_trigger_silences_the_port()
    {
        (RuntimeExecutable node, RuntimeExecution execution) = Node();
        await SubmitAsync(execution, Values(new PortValue("内容", false)));

        Assert.True(node.Triggers(Conclusion));

        node.Publish(execution);

        Assert.False(node.Triggers(Conclusion));
    }

    [Fact(DisplayName = "发布的 true 触发使端口发布")]
    public async Task Published_true_trigger_fires_the_port()
    {
        (RuntimeExecutable node, RuntimeExecution execution) = Node();
        await SubmitAsync(execution, Values(new PortValue("内容", true)));
        node.Publish(execution);

        Assert.True(node.Triggers(Conclusion));
    }

    [Fact(DisplayName = "缺少提交报告为未完成端口")]
    public async Task Missing_submission_is_reported_as_outstanding_ports()
    {
        (RuntimeExecutable node, RuntimeExecution execution) = Node();

        Assert.Equal([Conclusion], node.OutstandingPorts(execution));

        await SubmitAsync(execution, Values(new PortValue("内容")));

        Assert.Empty(node.OutstandingPorts(execution));
    }

    [Fact(DisplayName = "失效移除已发布的触发值")]
    public async Task Invalidate_removes_the_published_trigger_value()
    {
        (RuntimeExecutable node, RuntimeExecution execution) = Node();
        await SubmitAsync(execution, Values(new PortValue("内容", false)));
        node.Publish(execution);
        Assert.False(node.Triggers(Conclusion));

        node.Invalidate();

        Assert.True(node.Triggers(Conclusion));
    }

    private static (RuntimeExecutable Node, RuntimeExecution Exec) Node()
    {
        ExecutableNode executable = new()
        {
            Path = NodePath.Of("节点"),
            Type = TestNodeTypes.Executable,
            Spec = new ExecutableSpec(),
            Inputs = [],
            Outputs = [new Port(Conclusion, PortTypes.Text, mode: PortMode.Trigger)],
            MaxExecutions = ExecutableSpec.DefaultMaxExecutions,
        };
        GraphExecutionInput input = new()
        {
            Task = new TaskId(1),
            Node = executable.Path,
            Type = executable.Type,
            OutputPorts = executable.Outputs,
        };
        Kernel.Execution.ExecutionRuntime runtime = new(new Kernel.Execution.ExecutionDispatcher());
        Kernel.Execution.Shared.State.IExecution unit = runtime.Create(
            new Kernel.Execution.Shared.Identity.ExecutionId(1),
            RuntimeExecution.BuildRequest(input));
        RuntimeExecution execution = new(input, unit, 0, TestNodeCapabilities.Build());

        return (new RuntimeExecutable(executable, TestNodeCapabilities.Build()), execution);
    }

    private static Task<ErrorOr<Success>> SubmitAsync(
        RuntimeExecution execution,
        Dictionary<PortName, PortValue> values) =>
        new Kernel.Execution.ExecutionRuntime(new Kernel.Execution.ExecutionDispatcher())
            .RunAsync(
                execution.Unit,
                new ExecutionInvocation(
                    execution.Request.Step,
                    new SubmittingFactory(ToNeutralOutputs(values)),
                    EmptyExecutionParameters.Instance));

    private static Dictionary<PortName, PortValue> Values(PortValue value) =>
        new() { [Conclusion] = value };

    private static IReadOnlyList<ExecutionOutputValue> ToNeutralOutputs(
        IReadOnlyDictionary<PortName, PortValue> values) =>
        [
            .. values.Select(pair => new ExecutionOutputValue(
                new ExecutionPortName(pair.Key.Value),
                pair.Value.Text,
                pair.Value.Fires)),
        ];

    private sealed class SubmittingFactory(IReadOnlyList<ExecutionOutputValue> values) : IExecutionRunnerFactory
    {
        public IExecutionRunner Create(
            IExecutionParameters parameters,
            IExecutionRequestEntry requests) =>
            new Runner(requests, values);

        private sealed class Runner(
            IExecutionRequestEntry requests,
            IReadOnlyList<ExecutionOutputValue> values) : IExecutionRunner
        {
            public IProcessJournal Journal { get; } = new ExecutionJournal();

            public Task<ErrorOr<ExecutionOutput>> ExecuteAsync(CancellationToken cancellationToken)
            {
                ErrorOr<ExecutionStatus> submitted = requests.SubmitOutputs(values);

                return Task.FromResult<ErrorOr<ExecutionOutput>>(
                    submitted.IsError
                        ? submitted.ErrorsOrEmptyList
                        : ExecutionOutput.FromPorts([]));
            }
        }
    }
}
