using ErrorOr;
using Kuroe.Assistant.Flows;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Node.Llm;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话流程控制：等待输入与每一轮回话执行。</summary>
internal sealed class DialogueFlowControl(
    ITaskFlowContext task,
    IExecutionRuntime executions,
    IFlowExecutionGateway gateway,
    DialogueFlowState state) : IFlowControl
{
    /// <summary>回话步骤路径。</summary>
    internal static FlowStepPath ReplyStep { get; } = FlowStepPath.Of(DialogueFlow.ReplyNodeLabel.Value);

    private bool _awaiting = true;
    private IExecution? _live;

    /// <inheritdoc/>
    public Task Loop => Task.CompletedTask;

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
        lock (task.Gate)
        {
            if (_live is { IsLive: true } live)
            {
                executions.Interrupt(live, "任务暂停。");
            }
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
        return task.Terminate();
    }

    /// <inheritdoc/>
    public FlowSeed CaptureSeed() => _awaiting ? new FlowSeed([ReplyStep]) : FlowSeed.Empty;

    /// <inheritdoc/>
    public void InterruptForShutdown()
    {
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
        IExecution execution = gateway.Open(task, Request(text), executions.Create);
        task.AttachExecution(execution);
        _live = execution;
        _ = RunAsync(execution);

        return Result.Success;
    }

    private ExecutionRequest Request(string text) =>
        new(
            new ExecutionOwnerId(task.Id.Value),
            new ExecutionTargetId(DialogueFlow.ReplyNodeLabel.Value),
            LlmNodeTypes.Name.Value,
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
        try
        {
            IExecutionRunner runner = executions.CreateRunner(
                null,
                new DialogueTarget(new LlmNodeType(state.Parameters), state.Model),
                execution.Id);
            await executions.DispatchAsync(execution, runner, execution.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            executions.Fail(execution, [Error.Failure("Flow.Dispatch", $"对话执行失败：{ex.Message}")]);
        }

        lock (task.Gate)
        {
            if (ReferenceEquals(_live, execution))
            {
                _live = null;
            }

            _awaiting = true;
            task.NotifyExecutionSettled(execution);
            task.NotifyChanged();
        }
    }
}

/// <summary>对话执行的类型目标：Llm 种类与对话参数。</summary>
internal sealed class DialogueTarget(LlmNodeType type, string model) : IExecutionTarget
{
    /// <inheritdoc/>
    public ExecutionTargetId Id => new(DialogueFlow.ReplyNodeLabel.Value);

    /// <inheritdoc/>
    public string Kind => LlmNodeTypes.Name.Value;

    /// <inheritdoc/>
    public bool UsesDefaultRunner => true;

    /// <inheritdoc/>
    public IExecutionParameters Parameters { get; } = new LlmExecutionParameters(type, model);
}