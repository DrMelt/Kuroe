using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Input.Shared;
using Kuroe.Node.Llm;
using Kuroe.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue.Flow;

/// <summary>对话流程控制：等待输入与每一轮回话执行。</summary>
internal sealed class DialogueFlowControl(
    ITaskFlowContext task,
    ITaskExecutionGateway gateway,
    IInputChannel inputs,
    DialogueFlowState state) : IFlowControl
{
    private bool _awaiting = true;
    private string? _pendingInput;
    private IExecution? _live;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public Task Loop => Task.CompletedTask;

    /// <inheritdoc/>
    public Task Completion => _completion.Task;

    /// <inheritdoc/>
    public bool HasBlocked => false;

    /// <inheritdoc/>
    public bool HasWork => _awaiting || _pendingInput is not null || _live is not null;

    /// <inheritdoc/>
    public bool HasRestores => false;

    /// <inheritdoc/>
    public void Start() => Await();

    /// <inheritdoc/>
    public void Restore(FlowSeed seed)
    {
        if (seed.Steps.Count > 0)
        {
            Await();
        }
    }

    /// <inheritdoc/>
    public void Pause()
    {
        lock (task.Gate)
        {
            _awaiting = false;
            _pendingInput = null;
            inputs.Decline(task.Id, DialogueFlow.ReplyStep);
            if (_live is { IsLive: true } live)
            {
                gateway.Interrupt(live, "任务暂停。");
            }
        }
    }

    /// <inheritdoc/>
    public void Resume(bool continueRestores = true)
    {
        lock (task.Gate)
        {
            if (_live is not null)
            {
                _awaiting = false;
                inputs.Decline(task.Id, DialogueFlow.ReplyStep);
            }
            else
            {
                Await();
            }

            task.NotifyChanged();
        }
    }

    /// <inheritdoc/>
    public ErrorOr<Success> Terminate()
    {
        lock (task.Gate)
        {
            _awaiting = false;
            _pendingInput = null;
            inputs.Decline(task.Id, DialogueFlow.ReplyStep);
            ErrorOr<Success> result = task.Terminate();
            _completion.TrySetResult();

            return result;
        }
    }

    /// <inheritdoc/>
    public FlowSeed CaptureSeed()
    {
        lock (task.Gate)
        {
            return _awaiting || _pendingInput is not null
                ? DialogueFlow.ReplySeed
                : FlowSeed.Empty;
        }
    }

    /// <inheritdoc/>
    public void InterruptForShutdown()
    {
        lock (task.Gate)
        {
            _awaiting = false;
            _pendingInput = null;
            inputs.Decline(task.Id, DialogueFlow.ReplyStep);
            if (_live is { IsLive: true } live)
            {
                gateway.Interrupt(live, "进程退出。");
            }

            _completion.TrySetResult();
        }
    }

    /// <summary>登记回话步骤等待外部输入。</summary>
    private void Await()
    {
        _awaiting = true;
        inputs.Accept(
            task.Id,
            DialogueFlow.ReplyStep,
            FlowPendingKind.Input,
            DialogueFlow.Question,
            Receive);
    }

    /// <summary>收下一次外部输入，交付返回后建立回话执行。</summary>
    private void Receive(string text)
    {
        _awaiting = false;
        _pendingInput = text;
        _ = Task.Run(ContinueWithInput, CancellationToken.None);
    }

    /// <summary>按已收到的输入建立回话执行。交付在任务 Gate 内完成，这里在 Gate 释放后进入。</summary>
    private void ContinueWithInput()
    {
        IExecution? execution;
        DialogueFlowTarget target;
        lock (task.Gate)
        {
            if (_pendingInput is not { } input
                || _awaiting
                || _live is not null
                || task.Lifecycle != TaskLifecycle.Active)
            {
                return;
            }

            _pendingInput = null;
            execution = gateway.Open(task, Request(input));
            IExecution? previous = task.Executions.Count > 0 ? task.Executions[^1] : null;
            target = new DialogueFlowTarget(state.Parameters, state.Model, previous?.RestoreData);
            task.AttachExecution(execution);
            _live = execution;
        }

        _ = RunAsync(execution, target);
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

    private async Task RunAsync(IExecution execution, DialogueFlowTarget target)
    {
        await gateway.RunAsync(
            execution,
            null,
            target).ConfigureAwait(false);

        lock (task.Gate)
        {
            if (ReferenceEquals(_live, execution))
            {
                _live = null;
            }

            if (task.Lifecycle == TaskLifecycle.Active)
            {
                Await();
            }
            else
            {
                _awaiting = false;
                inputs.Decline(task.Id, DialogueFlow.ReplyStep);
            }

            task.NotifyExecutionSettled(execution);
            task.NotifyChanged();
        }
    }
}