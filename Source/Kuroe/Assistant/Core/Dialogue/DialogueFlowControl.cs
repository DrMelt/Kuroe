using ErrorOr;
using Kuroe.Assistant.Flows;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Tasks;
using Kuroe.Node.Llm;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话流程控制：等待输入与每一轮回话执行。</summary>
internal sealed class DialogueFlowControl(
    ITaskFlowContext task,
    IFlowExecutionGateway gateway,
    DialogueFlowState state) : IFlowControl, IFlowInputControl
{
    /// <summary>回话步骤路径。</summary>
    internal static FlowStepPath ReplyStep { get; } = FlowStepPath.Of(DialogueFlow.ReplyLabel);

    private bool _awaiting = true;
    private IExecution? _live;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public Task Loop => Task.CompletedTask;

    /// <inheritdoc/>
    public Task Completion => _completion.Task;

    /// <inheritdoc/>
    public bool HasBlocked => false;

    /// <inheritdoc/>
    public bool HasWork => true;

    /// <inheritdoc/>
    public bool HasRestores => false;

    /// <inheritdoc/>
    public void Start() => _awaiting = true;

    /// <inheritdoc/>
    public void Restore(FlowSeed seed) => _awaiting = seed.Steps.Count > 0;

    /// <inheritdoc/>
    public void Pause()
    {
        _awaiting = false;
        if (_live is { IsLive: true } live)
        {
            gateway.Interrupt(live, "任务暂停。");
        }
    }

    /// <inheritdoc/>
    public void Advance(IReadOnlyList<FlowStepPath> steps)
    {
    }

    /// <inheritdoc/>
    public void Resume(bool continueRestores = true)
    {
        _awaiting = true;
        task.NotifyChanged();
    }

    /// <inheritdoc/>
    public int Rerun() => 0;

    /// <inheritdoc/>
    public ErrorOr<Success> Terminate()
    {
        _awaiting = false;
        ErrorOr<Success> result = task.Terminate();
        _completion.TrySetResult();

        return result;
    }

    /// <inheritdoc/>
    public FlowSeed CaptureSeed() => _awaiting ? new FlowSeed([ReplyStep]) : FlowSeed.Empty;

    /// <inheritdoc/>
    public void InterruptForShutdown()
    {
        _awaiting = false;
        lock (task.Gate)
        {
            if (_live is { IsLive: true } live)
            {
                gateway.Interrupt(live, "进程退出。");
            }
        }

        _completion.TrySetResult();
    }

    /// <inheritdoc/>
    public IReadOnlyList<FlowPendingStep> Pending() =>
        _awaiting
            ? [new FlowPendingStep(ReplyStep, FlowPendingKind.Input, DialogueFlow.Question)]
            : [];

    /// <inheritdoc/>
    public ErrorOr<Success> Deliver(FlowStepPath step, string text)
    {
        if (!_awaiting)
        {
            return [Error.Conflict("Flow.Deliver", "对话不在等待输入。")];
        }

        if (step != ReplyStep)
        {
            return [Error.Validation("Flow.Deliver", $"对话没有步骤 {step.Value}。")];
        }

        _awaiting = false;
        IExecution execution = gateway.Open(task, Request(text));
        task.AttachExecution(execution);
        _live = execution;
        _ = RunAsync(execution);

        return Result.Success;
    }

    private ExecutionRequest Request(string text) =>
        new(
            new ExecutionOwnerId(task.Id.Value),
            new ExecutionTargetId(DialogueFlow.ReplyLabel),
            LlmNodeTypes.Kind,
            1,
            [],
            [new ExecutionInputValue(new ExecutionPortName(DialogueFlow.InputPort.Value), text, null)],
            new Dictionary<ExecutionPortName, string>
            {
                [new ExecutionPortName(DialogueFlow.WorkspacePort.Value)] = state.Workspace,
            },
            []);

    private async Task RunAsync(IExecution execution)
    {
        await gateway.RunAsync(
            execution,
            null,
            new DialogueTarget(state.Parameters, state.Model)).ConfigureAwait(false);

        lock (task.Gate)
        {
            if (ReferenceEquals(_live, execution))
            {
                _live = null;
            }

            _awaiting = task.Lifecycle != TaskLifecycle.Terminated;
            task.NotifyExecutionSettled(execution);
            task.NotifyChanged();
        }
    }
}

/// <summary>对话执行的类型目标：Llm 种类与对话参数。</summary>
internal sealed class DialogueTarget(LlmParameters parameters, string model) : IExecutionTarget
{
    /// <inheritdoc/>
    public ExecutionTargetId Id => new(DialogueFlow.ReplyLabel);

    /// <inheritdoc/>
    public string Kind => LlmNodeTypes.Kind;

    /// <inheritdoc/>
    public bool UsesDefaultRunner => true;

    /// <inheritdoc/>
    public IExecutionParameters Parameters { get; } = new LlmExecutionParameters(parameters, model, LlmNodeTypes.Kind);
}