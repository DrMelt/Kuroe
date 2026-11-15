using ErrorOr;
using Kuroe.Assistant.Flows;
using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Input.Shared;
using Kuroe.Tasks.Shared;
using Kuroe.Node.Llm;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话流程控制：等待输入与每一轮回话执行。</summary>
internal sealed class DialogueFlowControl(
    ITaskFlowContext task,
    ITaskExecutionGateway gateway,
    IInputChannel inputs,
    DialogueFlowState state) : IFlowControl
{
    /// <summary>回话步骤路径。</summary>
    internal static FlowStepPath ReplyStep { get; } = FlowStepPath.Of(DialogueFlow.ReplyLabel);

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
            inputs.Decline(task.Id, ReplyStep);
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
                inputs.Decline(task.Id, ReplyStep);
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
            inputs.Decline(task.Id, ReplyStep);
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
                ? new FlowSeed([ReplyStep])
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
            inputs.Decline(task.Id, ReplyStep);
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
            ReplyStep,
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
        DialogueTarget target;
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
            target = new DialogueTarget(state.Parameters, state.Model, previous?.RestoreData);
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

    private async Task RunAsync(IExecution execution, DialogueTarget target)
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
                inputs.Decline(task.Id, ReplyStep);
            }

            task.NotifyExecutionSettled(execution);
            task.NotifyChanged();
        }
    }
}

/// <summary>对话执行的类型目标：Llm 种类、对话参数与上一轮会话快照。</summary>
internal sealed class DialogueTarget(
    LlmParameters parameters,
    string model,
    ExecutionRestoreState? seed) : IExecutionTarget
{
    /// <inheritdoc/>
    public ExecutionTargetId Id => new(DialogueFlow.ReplyLabel);

    /// <inheritdoc/>
    public string Kind => LlmNodeTypes.Kind;

    /// <inheritdoc/>
    public bool UsesDefaultRunner => true;

    /// <inheritdoc/>
    public IExecutionParameters Parameters { get; } = new LlmExecutionParameters(parameters, model, LlmNodeTypes.Kind)
    {
        ConversationSeed = seed,
    };
}