using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Workflows.Tasks;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;
using Xunit;

namespace Kuroe.Tests;

/// <summary>输入消费账本：每条入边独立记账，同源多条目标记边互不覆盖；信号边独立按来源版本驱动。</summary>
public sealed class NodeInputLedgerTests
{
    /// <summary>同源 PerItem 数据边与信号边共享来源：数据按实例集一次性消费，信号按版本驱动。
    /// 来源再次发布时信号必须重新就绪，与两边在目标 From 里的声明顺序无关。</summary>
    [Fact]
    public void Same_source_data_and_signal_keep_independent_ledgers()
    {
        foreach (bool signalFirst in new[] { true, false })
        {
            Run(signalFirst);
        }
    }

    private static void Run(bool signalFirst)
    {
        ExecutableNode sourceDef = SourceDefinition();
        FlowEdge data = new(0, 1, EdgeFeed.AllInstances, OutputPort);
        FlowEdge trigger = new(0, 1, EdgeFeed.Single, OutputPort, Role: EdgeRole.Trigger);
        NodeGraph graph = new([sourceDef, TargetDefinition()], signalFirst ? [trigger, data] : [data, trigger]);

        RuntimeExecutable source = new(sourceDef);
        RuntimeExecutable target = new(TargetDefinition());
        WorkTask task = new(new TaskId(1), "目标", DefaultFlow(), graph, null);
        NodeInput input = NodeInput.Build(task, target, index => index == 0 ? source : target);

        source.AdoptItems([0, 1]);
        source.Publish(0);
        source.Publish(1);

        Assert.True(input.Ready);
        input.Consume();
        Assert.False(input.Ready);

        // 来源再次发布：信号账本独立按版本记账，目标重新就绪
        source.Publish(0);
        Assert.True(input.Ready);
    }

    private static FlowDefinition DefaultFlow() =>
        new(new FlowName("默认"), null, [], new NodeSpec { Name = new NodeName("整体") });

    private static ExecutableNode SourceDefinition() => new()
    {
        Index = 0,
        Name = new NodeName("来源"),
        Path = new NodePath([new NodeName("来源")]),
        Gate = NodeGate.Auto,
        Execution = new ExecutableSpec { Output = NodeOutput.Text, Mode = NodeMode.PerItem },
        Model = Model,
        From = [],
        Outputs = [],
        SystemPrompt = [],
        MaxRuns = 100,
    };

    private static ExecutableNode TargetDefinition() => new()
    {
        Index = 1,
        Name = new NodeName("目标"),
        Path = new NodePath([new NodeName("目标")]),
        Gate = NodeGate.Auto,
        Execution = new ExecutableSpec { Output = NodeOutput.Text, Mode = NodeMode.Single },
        Model = Model,
        From =
        [
            new Dependency(0, OutputPort),
            new Dependency(0, OutputPort, Signal: true),
        ],
        Outputs = [],
        SystemPrompt = [],
        MaxRuns = 100,
    };

    private static readonly ModelDefinition Model = new() { Name = new ModelRef("执行者") };

    private static readonly PortName OutputPort = new("结论");
}
