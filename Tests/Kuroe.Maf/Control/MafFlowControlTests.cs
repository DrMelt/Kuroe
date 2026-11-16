using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Execution;
using Kuroe.Execution.Shared;
using Kuroe.Control.Runtime;
using Kuroe.Tasks.Runtime;
using Kuroe.Tasks.Runtime.Persistence;
using Kuroe.Maf.Records.Shared;
using Kuroe.Presentation.Shared;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Maf.Control.Tests;

/// <summary>MAF 流程控制：MAF 运行期推进步骤，每次步骤运行产生一次 Kuroe 执行。</summary>
public sealed class MafFlowControlTests
{
    private static readonly string[] ForkedSteps = ["起点", "左", "右"];

    [Fact]
    public async Task Control_runs_every_step_and_completes_the_task()
    {
        Harness harness = new();
        harness.Start();

        await WaitUntil(() => harness.Work.State == TaskState.Done);

        Assert.Equal(2, harness.Work.Executions.Count);
        Assert.All(harness.Work.Executions, execution => Assert.Equal(ExecutionState.Succeeded, execution.State));
        Assert.Equal(["第一步", "第二步"], harness.Work.Executions.Select(execution => execution.Request.Target.Value));
    }

    [Fact]
    public async Task Control_leaves_a_checkpoint_for_the_archive()
    {
        Harness harness = new();
        harness.Start();

        await WaitUntil(() => harness.Work.State == TaskState.Done);

        FlowRecord record = harness.Work.UnderLock(() => new MafFlowRecordSource().CaptureRecord(harness.Work));
        MafFlowStateRecord state = Assert.IsType<MafFlowStateRecord>(record.State);

        Assert.False(string.IsNullOrEmpty(state.CheckpointId));
        Assert.False(string.IsNullOrEmpty(state.CheckpointData));
        Assert.Empty(state.PendingSteps);
    }

    [Fact]
    public void View_lists_definition_steps_and_start_ports()
    {
        Harness harness = new();

        TaskSnapshot snapshot = harness.Work.Snapshot();

        Assert.Equal("测试", snapshot.View.Name);
        Assert.Equal(["第一步", "第二步"], snapshot.View.Steps.Select(step => step.Label));
        Assert.Equal(["目标", "MODEL_NAME"], snapshot.View.StartPorts.Select(port => port.Name));
    }

    [Fact]
    public async Task Interrupted_step_pauses_the_task_and_resume_continues()
    {
        int created = 0;
        FakeRunnerFactory runners = new(() => new FakeRunner
        {
            Block = Interlocked.Increment(ref created) == 1,
        });
        Harness harness = new(runners, TestFlow.SingleStep());
        harness.Start();

        await WaitForRunner(runners, 1);
        ExecutionId execution = Assert.Single(harness.Work.Executions).Id;
        Assert.False(harness.Stop(execution).IsError);

        await WaitUntil(() => harness.Work.Lifecycle == TaskLifecycle.Paused);
        Assert.Equal(TaskState.Paused, harness.Work.State);

        harness.Resume();
        await WaitUntil(() => harness.Work.State == TaskState.Done);

        Assert.Contains(harness.Work.Executions, candidate => candidate.State == ExecutionState.Interrupted);
        Assert.Contains(harness.Work.Executions, candidate => candidate.State == ExecutionState.Succeeded);
    }

    [Fact]
    public async Task Shutdown_waits_for_the_loop_to_stop()
    {
        FakeRunnerFactory runners = new(() => new FakeRunner { Block = true });
        Harness harness = new(runners, TestFlow.SingleStep());
        harness.Start();
        await WaitForRunner(runners, 1);

        await harness.Flow.ShutdownAsync();

        Assert.Equal(0, harness.Flow.ActiveControls);
        Assert.Equal(TaskLifecycle.Active, harness.Work.Lifecycle);
        Assert.All(harness.Work.Executions,
            execution => Assert.Equal(ExecutionState.Interrupted, execution.State));
    }

    [Fact]
    public async Task Rerun_restarts_a_failed_flow_from_the_entry()
    {
        int created = 0;
        FakeRunnerFactory runners = new(() => new FakeRunner
        {
            Failure = Interlocked.Increment(ref created) == 1
                ? Error.Failure("Test.Failure", "第一步失败")
                : null,
        });
        Harness harness = new(runners, TestFlow.SingleStep());
        harness.Start();

        await WaitUntil(() => harness.Work.State == TaskState.Blocked);
        Assert.Equal(1, harness.Rerun());
        await WaitUntil(() => harness.Work.State == TaskState.Done);

        Assert.Contains(harness.Work.Executions, execution => execution.State == ExecutionState.Failed);
        Assert.Contains(harness.Work.Executions, execution => execution.State == ExecutionState.Succeeded);
    }

    [Fact]
    public async Task View_marks_a_failed_step_as_blocked()
    {
        FakeRunnerFactory runners = new(() => new FakeRunner
        {
            Failure = Error.Failure("Test.Failure", "失败"),
        });
        Harness harness = new(runners, TestFlow.SingleStep());
        harness.Start();

        await WaitUntil(() => harness.Work.State == TaskState.Blocked);
        FlowStepView step = Assert.Single(harness.Work.Snapshot().View.Steps);

        Assert.Equal(FlowItemState.Blocked, step.State);
        Assert.Equal("失败", step.Block);
    }

    [Fact]
    public async Task Restore_rebuilds_state_executions_and_checkpoint()
    {
        Harness harness = new();
        harness.Start();
        await WaitUntil(() => harness.Work.State == TaskState.Done);

        ErrorOr<TaskRecord> captured = new TaskRecordCapture(harness.Work).CaptureRecord();
        Assert.False(captured.IsError);

        MafFlowSubmission submission = new(new MafFlowCatalog([TestFlow.Definition()]), harness.Gateway);
        ErrorOr<FlowRestore> restored = submission.Restore(captured.Value);

        Assert.False(restored.IsError);
        Assert.Equal(2, restored.Value.Executions.Count);
        Assert.Equal(2, restored.Value.StartTexts.Texts.Count);
        MafFlowStateRecord state = Assert.IsType<MafFlowStateRecord>(captured.Value.Flow.State);
        Assert.Equal("test-model", state.Model);
        Assert.False(string.IsNullOrEmpty(state.CheckpointId));
    }

    [Fact]
    public void Templates_expose_start_ports_and_steps()
    {
        MafFlowTemplates templates = new(new MafFlowCatalog([TestFlow.Definition()]));
        FlowTemplateView template = Assert.Single(templates.All());

        Assert.Equal("测试", template.Name);
        Assert.Equal(["目标", "MODEL_NAME"], template.StartPorts.Select(port => port.Name));
        Assert.Equal(["第一步", "第二步"], template.Steps.Select(step => step.Label));
    }

    [Fact]
    public async Task Control_runs_forked_steps()
    {
        Harness harness = new(definition: TestFlow.Fork());
        harness.Start();

        await WaitUntil(() => harness.Work.State == TaskState.Done);

        Assert.Equal(
            ForkedSteps.Order(StringComparer.Ordinal),
            harness.Work.Executions.Select(execution => execution.Request.Target.Value).Order(StringComparer.Ordinal));
        Assert.All(harness.Work.Executions, execution => Assert.Equal(ExecutionState.Succeeded, execution.State));
    }

    [Fact]
    public async Task Terminate_keeps_unfinished_steps()
    {
        FakeRunnerFactory runners = new(() => new FakeRunner { Block = true });
        Harness harness = new(runners, TestFlow.Definition());
        harness.Start();
        await WaitForRunner(runners, 1);
        await WaitUntil(() => harness.Pending().SequenceEqual(["第二步"]));

        Assert.False(harness.Terminate().IsError);
        await WaitUntil(() => harness.Flow.ActiveControls == 0);

        Assert.Equal(TaskLifecycle.Terminated, harness.Work.Lifecycle);
        Assert.Equal(["第二步"], harness.Pending());
        Assert.DoesNotContain(
            harness.Work.JournalEntries,
            entry => entry.Text.Contains("可恢复阻塞", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Pause_keeps_the_task_paused_and_resumes_from_entry()
    {
        int created = 0;
        FakeRunnerFactory runners = new(() => new FakeRunner
        {
            Block = Interlocked.Increment(ref created) == 1,
        });
        Harness harness = new(runners, TestFlow.Definition());
        harness.Start();
        await WaitForRunner(runners, 1);

        Assert.False(harness.Pause().IsError);
        await WaitUntil(() => harness.Work.State == TaskState.Paused);
        await WaitUntil(() => harness.Pending().Length == 2);

        harness.Resume();
        await WaitUntil(() => harness.Work.State == TaskState.Done);

        Assert.Equal(2, harness.Work.Executions.Count(execution => execution.Request.Target.Value == "第一步"));
        Assert.Single(harness.Work.Executions, execution => execution.Request.Target.Value == "第二步");
        Assert.DoesNotContain(
            harness.Work.JournalEntries,
            entry => entry.Text.Contains("可恢复阻塞", StringComparison.Ordinal));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 250; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("任务没有在预期时间内到达预期状态。");
    }

    private static async Task<FakeRunner> WaitForRunner(FakeRunnerFactory runners, int index)
    {
        for (int attempt = 0; attempt < 250; attempt++)
        {
            IReadOnlyList<FakeRunner> created = runners.Runners;
            if (created.Count >= index && created[index - 1].Started.Task.IsCompleted)
            {
                return created[index - 1];
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("没有等到执行运行。");
    }

    private sealed class Harness
    {
        internal Harness(FakeRunnerFactory? runners = null, MafFlowDefinition? definition = null)
        {
            FakeRunnerFactory factory = runners ?? new FakeRunnerFactory(() => new FakeRunner());
            MafFlowDefinition flow = definition ?? TestFlow.Definition();
            MafFlowCatalog flows = new([flow]);
            ExecutionIndex index = new();
            IExecutionRuntime executions = new ExecutionRuntime(
                new ExecutionDispatcher(),
                new ExecutionRunnerRegistry([new ExecutionRunnerRegistration(typeof(FakeRunnerFactory), factory)]));
            TaskRegistry registry = new(index, pauseOnRestore: false);
            Gateway = new TaskExecutionGateway(index, executions);
            Flow = new FlowRuntime(executions);
            FlowProvider provider = new(
                new MafFlowIdentity(flows),
                new MafFlowSubmission(flows, Gateway),
                null,
                new MafFlowControlFactory(Gateway),
                new MafFlowPresentation(),
                new MafFlowRecordSource());

            FlowSubmission submission = new MafFlowSubmission(flows, Gateway)
                .Prepare(new TaskId(1), TestFlow.Name, TestFlow.Inputs(), "标题")
                .Value;
            Work = new WorkTask(
                registry,
                executions,
                new TaskId(1),
                submission.State.Inputs,
                submission.StartTexts,
                submission.Title,
                provider,
                submission.State,
                submission.Definition,
                observer: null);
            registry.Adopt(Work);
        }

        internal TaskExecutionGateway Gateway { get; }

        internal FlowRuntime Flow { get; }

        internal WorkTask Work { get; }

        internal void Start() =>
            Work.UnderLock(() =>
            {
                Flow.Start(Work);
                return true;
            });

        internal ErrorOr<Success> Stop(ExecutionId id) =>
            Work.UnderLock(() => Flow.StopExecution(Work, id));

        internal ErrorOr<Success> Pause() =>
            Work.UnderLock(() =>
            {
                ErrorOr<Success> paused = ((ITaskFlowContext)Work).Pause();
                if (!paused.IsError)
                {
                    Flow.Pause(Work);
                }

                return paused;
            });

        internal ErrorOr<Success> Terminate() => Work.UnderLock(() => Flow.Terminate(Work));

        internal string[] Pending() =>
            Work.UnderLock(() =>
                ((MafFlowStateRecord)new MafFlowRecordSource().CaptureRecord(Work).State).PendingSteps.ToArray());

        internal void Resume() =>
            Work.UnderLock(() =>
            {
                ((ITaskFlowContext)Work).Resume();
                Flow.ResumeTask(Work);
                return true;
            });

        internal int Rerun() => Work.UnderLock(() => Flow.Rerun(Work));
    }
}