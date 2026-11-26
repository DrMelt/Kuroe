using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Maf.Definition;
using Xunit;

namespace Kuroe.Maf.Control.Tests;

/// <summary>步骤节点与开始执行器：多入口、汇合、组 OR、反向触发与单次执行。</summary>
public sealed class MafStepNodeTests
{
    private static readonly PortName Output = new("输出");
    private static readonly PortName InputName = new("输入");

    [Fact(DisplayName = "开始执行器向多个入口发送消息")]
    public async Task Start_executor_starts_every_entry()
    {
        RunRecorder recorder = new();
        MafStep first = Step("甲", new EmptyPlans(), recorder);
        MafStep second = Step("乙", new EmptyPlans(), recorder);
        MafFlowDefinition definition = Definition([first, second], []);

        await RunAsync(definition, recorder);

        Assert.Equal(1, recorder.Count("甲"));
        Assert.Equal(1, recorder.Count("乙"));
    }

    [Fact(DisplayName = "汇合步骤等待全部组来源并聚合文本")]
    public async Task Join_step_waits_for_all_sources_and_aggregates_text()
    {
        RunRecorder recorder = new();
        MafStep first = Step("甲", new EmptyPlans(), recorder);
        MafStep second = Step("乙", new EmptyPlans(), recorder);
        MafStep join = Step("汇合", new JoinPlans(first.Id, second.Id), recorder);
        MafFlowDefinition definition = Definition(
            [first, second, join],
            [Edge(first.Id, join.Id), Edge(second.Id, join.Id)]);

        await RunAsync(definition, recorder);

        IReadOnlyList<MafStepInput> inputs = Assert.Single(recorder.Inputs("汇合"));
        Assert.Equal(["甲", "乙"], inputs.Select(input => input.Text).Order());
    }

    [Fact(DisplayName = "组 OR 先满足的组启动步骤且只执行一次")]
    public async Task Group_or_starts_once_from_the_first_ready_group()
    {
        RunRecorder recorder = new();
        MafStep first = Step("甲", new EmptyPlans(), recorder);
        MafStep second = Step("乙", new EmptyPlans(), recorder);
        MafStep join = Step("选择", new JoinPlans(first.Id, second.Id, orGroups: true), recorder);
        MafFlowDefinition definition = Definition(
            [first, second, join],
            [Edge(first.Id, join.Id), Edge(second.Id, join.Id)]);

        await RunAsync(definition, recorder);

        IReadOnlyList<MafStepInput> inputs = Assert.Single(recorder.Inputs("选择"));
        Assert.Equal("甲", Assert.Single(inputs).Text);
        Assert.Equal(1, recorder.Count("选择"));
    }

    [Fact(DisplayName = "反向触发条件接受未触发的来源")]
    public async Task Negative_condition_accepts_a_source_that_does_not_fire()
    {
        RunRecorder recorder = new(fires: false);
        MafStep first = Step("甲", new EmptyPlans(), recorder);
        MafStep second = Step("乙", new EmptyPlans(), recorder);
        MafStep join = Step(
            "汇合",
            new JoinPlans(first.Id, second.Id, negative: true),
            recorder);
        MafFlowDefinition definition = Definition(
            [first, second, join],
            [Edge(first.Id, join.Id), Edge(second.Id, join.Id)]);

        await RunAsync(definition, recorder);

        Assert.Equal(1, recorder.Count("汇合"));
        Assert.All(
            Assert.Single(recorder.Inputs("汇合")),
            input => Assert.Equal(MafTriggerCondition.Negative, input.Condition));
    }

    private static MafStep Step(string id, IMafStepInputAssembler inputs, RunRecorder recorder)
    {
        _ = recorder;

        return new MafStep(
            new MafStepId(id),
            id,
            [Output],
            TestFlow.Invocation(TestFlow.Factory),
            inputs)
        {
            OutputPlan = [new MafOutputPort(Output, ExecutionPortKind.Text)],
        };
    }

    private static MafFlowDefinition Definition(
        IReadOnlyList<MafStep> steps,
        IReadOnlyList<MafEdge> edges) =>
        new(
            new FlowName("节点测试"),
            [new MafStartPort(MafFlowDefinition.ModelPort)],
            steps,
            edges);

    private static MafEdge Edge(MafStepId from, MafStepId to) => new(from, to);

    private static async Task RunAsync(MafFlowDefinition definition, RunRecorder recorder)
    {
        MemoryCheckpoints checkpoints = new();
        SequenceCheckpointIds ids = new();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));

        await using IMafWorkflowRun run = await MafWorkflowRunner.StartAsync(
            definition,
            step => Node(step, recorder),
            "test",
            checkpoints,
            ids,
            timeout.Token).ConfigureAwait(false);

        await foreach (MafWorkflowEvent update in run.WatchAsync(timeout.Token).ConfigureAwait(false))
        {
            Consume(update);
        }
    }

    private static MafStepNode Node(MafStep step, RunRecorder recorder) =>
        new(
            step,
            upstream => step.Inputs.Assemble(new MafStepInputContext(
                new ExecutionOwnerId("1"),
                step.Id,
                upstream,
                new Dictionary<PortName, string>(),
                "test-model")),
            (current, inputs, _) =>
            {
                recorder.Record(current.Id, inputs);

                return ValueTask.FromResult(new MafStepMessage(
                    current.Id,
                    [
                        new MafStepOutput(current.Id, Output, current.Id.Value)
                        {
                            Fires = recorder.Fires,
                        },
                    ]));
            });

    private static void Consume(MafWorkflowEvent update) => _ = update;

    private sealed class RunRecorder(bool fires = true)
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<string, List<IReadOnlyList<MafStepInput>>> _runs = [];

        internal bool Fires { get; } = fires;

        internal void Record(MafStepId step, IReadOnlyList<MafStepInput> inputs)
        {
            lock (_gate)
            {
                if (!_runs.TryGetValue(step.Value, out List<IReadOnlyList<MafStepInput>>? values))
                {
                    values = [];
                    _runs[step.Value] = values;
                }

                values.Add(inputs);
            }
        }

        internal int Count(string step)
        {
            lock (_gate)
            {
                return _runs.TryGetValue(step, out List<IReadOnlyList<MafStepInput>>? values) ? values.Count : 0;
            }
        }

        internal IReadOnlyList<IReadOnlyList<MafStepInput>> Inputs(string step)
        {
            lock (_gate)
            {
                return _runs.TryGetValue(step, out List<IReadOnlyList<MafStepInput>>? values)
                    ? [.. values]
                    : [];
            }
        }
    }

    private sealed class EmptyPlans : IMafStepInputAssembler
    {
        public IReadOnlyList<MafStepInput> Assemble(MafStepInputContext context) => [];
    }

    private sealed class JoinPlans(
        MafStepId first,
        MafStepId second,
        bool orGroups = false,
        bool negative = false) : IMafStepInputAssembler
    {
        public IReadOnlyList<MafStepInput> Assemble(MafStepInputContext context) =>
        [
            Input(context, first, 0),
            Input(context, second, orGroups ? 1 : 0),
        ];

        private MafStepInput Input(MafStepInputContext context, MafStepId source, int group)
        {
            MafStepOutput? output = context.Upstream.FirstOrDefault(candidate =>
                candidate.Step == source && candidate.Port == Output);

            return new MafStepInput(
                InputName,
                ExecutionPortKind.Text,
                ExecutionPortRole.Content,
                Required: true,
                DefaultText: null,
                Source: null,
                SourceKey: null,
                output?.Text ?? string.Empty,
                source)
            {
                Group = group,
                UpstreamPort = Output,
                Condition = negative ? MafTriggerCondition.Negative : MafTriggerCondition.Positive,
            };
        }
    }

    private sealed class MemoryCheckpoints : IMafWorkflowCheckpoints
    {
        public MafWorkflowCheckpoint? Load() => null;

        public void Save(MafWorkflowCheckpoint checkpoint)
        {
        }
    }

    private sealed class SequenceCheckpointIds : ICheckpointIdSource
    {
        private int _value;

        public string Next() => $"检查点-{++_value}";
    }
}