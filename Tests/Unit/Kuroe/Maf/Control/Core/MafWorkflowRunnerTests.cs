using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Maf.Definition;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.Maf.Control.Tests;

/// <summary>运行器：按定义构建 workflow，运行步骤并交出运行事件。</summary>
public sealed class MafWorkflowRunnerTests
{
    [Fact(DisplayName = "运行器运行定义报告步骤事件并按给定键保存检查点")]
    public async Task Runner_runs_the_definition_and_reports_step_events()
    {
        MafFlowDefinition definition = Definition();
        List<MafWorkflowEvent> events = [];
        MemoryCheckpoints checkpoints = new();
        SequenceCheckpointIds ids = new();

        await using IMafWorkflowRun run = await MafWorkflowRunner.StartAsync(
            definition,
            Node,
            "test",
            checkpoints,
            ids,
            CancellationToken.None);

        await foreach (MafWorkflowEvent update in run.WatchAsync(CancellationToken.None))
        {
            events.Add(update);
        }

        Assert.Contains(events, update => update is MafStepStarted started && started.Step.Id.Value == "第一步");
        Assert.Contains(events, update => update is MafStepCompleted completed && completed.Step.Id.Value == "第一步");
        Assert.Contains(events, update => update is MafStepStarted started && started.Step.Id.Value == "第二步");
        Assert.Contains(events, update => update is MafStepCompleted completed && completed.Step.Id.Value == "第二步");

        MafWorkflowCheckpoint? saved = checkpoints.Load();
        Assert.NotNull(saved);
        Assert.Equal(ids.Issued[^1], saved.Id);
    }

    private static MafStepNode Node(MafStep step) =>
        new(
            step,
            upstream => step.Inputs.Assemble(new MafStepInputContext(
                new ExecutionOwnerId("1"),
                step.Id,
                upstream,
                new Dictionary<PortName, string>(),
                "test-model")),
            (current, _, _) => ValueTask.FromResult(new MafStepMessage(
                current.Id,
                [new MafStepOutput(current.Id, new PortName("输出"), current.Id.Value)])));

    private static MafFlowDefinition Definition() =>
        new(
            new FlowName("测试"),
            [new MafStartPort(new PortName("目标")), new MafStartPort(MafFlowDefinition.ModelPort)],
            [
                new MafStep(new MafStepId("第一步"), "第一步", [new PortName("输出")], Invocation(), MafUpstreamInputs.Instance),
                new MafStep(new MafStepId("第二步"), "第二步", [new PortName("输出")], Invocation(), MafUpstreamInputs.Instance),
            ],
            [new MafEdge(new MafStepId("第一步"), new MafStepId("第二步"))]);

    private static MafStepInvocationFactory Invocation() =>
        TestFlow.Invocation(TestFlow.Factory);

    private sealed class MemoryCheckpoints : IMafWorkflowCheckpoints
    {
        private MafWorkflowCheckpoint? _checkpoint;

        public MafWorkflowCheckpoint? Load() => _checkpoint;

        public void Save(MafWorkflowCheckpoint checkpoint) => _checkpoint = checkpoint;
    }

    /// <summary>按序给出检查点键并记录已发出的键。</summary>
    private sealed class SequenceCheckpointIds : ICheckpointIdSource
    {
        public List<string> Issued { get; } = [];

        public string Next()
        {
            string key = $"检查点-{Issued.Count + 1}";
            Issued.Add(key);
            return key;
        }
    }
}