using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;
using Kuroe.Maf.Definition;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Maf.Control.Tests;

/// <summary>运行器：按定义构建 workflow，运行步骤并交出运行事件。</summary>
public sealed class MafWorkflowRunnerTests
{
    [Fact]
    public async Task Runner_runs_the_definition_and_reports_step_events()
    {
        MafFlowDefinition definition = Definition();
        MafStep entry = definition.Entry().Value;
        List<MafWorkflowEvent> events = [];

        await using IMafWorkflowRun run = await MafWorkflowRunner.StartAsync(
            definition,
            new MafStepMessage(entry.Id, []),
            "test",
            Handlers,
            new MemoryCheckpoints(),
            CancellationToken.None);

        await foreach (MafWorkflowEvent update in run.WatchAsync(CancellationToken.None))
        {
            events.Add(update);
        }

        Assert.Contains(events, update => update is MafStepStarted started && started.Step.Id.Value == "第一步");
        Assert.Contains(events, update => update is MafStepCompleted completed && completed.Step.Id.Value == "第一步");
        Assert.Contains(events, update => update is MafStepStarted started && started.Step.Id.Value == "第二步");
        Assert.Contains(events, update => update is MafStepCompleted completed && completed.Step.Id.Value == "第二步");
    }

    private static MafStepHandler Handlers(MafStep step) =>
        (message, _) => ValueTask.FromResult(new MafStepMessage(
            step.Id,
            [new MafStepOutput(step.Id, new PortName("输出"), message.Step.Value)]));

    private static MafFlowDefinition Definition() =>
        new(
            new FlowName("测试"),
            [new MafStartPort(new PortName("目标")), new MafStartPort(MafFlowDefinition.ModelPort)],
            [
                new MafStep(new MafStepId("第一步"), "第一步", [new PortName("输出")], Target()),
                new MafStep(new MafStepId("第二步"), "第二步", [new PortName("输出")], Target()),
            ],
            [new MafEdge(new MafStepId("第一步"), new MafStepId("第二步"))]);

    private static MafStepTarget Target() =>
        new(new ExecutionRunnerKey("Test"), _ => EmptyExecutionParameters.Instance);

    private sealed class MemoryCheckpoints : IMafWorkflowCheckpoints
    {
        private MafWorkflowCheckpoint? _checkpoint;

        public MafWorkflowCheckpoint? Load() => _checkpoint;

        public void Save(MafWorkflowCheckpoint checkpoint) => _checkpoint = checkpoint;
    }
}