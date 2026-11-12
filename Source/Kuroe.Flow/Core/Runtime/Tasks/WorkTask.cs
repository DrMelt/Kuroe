using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Runtime.Process;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Tasks.Records;
using LoopExecution = Kuroe.Flow.Execution.Execution;
using ExecutionId = Kuroe.Flow.Execution.ExecutionId;
using ExecutionRequest = Kuroe.Flow.Execution.ExecutionRequest;
using ExecutionSnapshot = Kuroe.Flow.Execution.ExecutionSnapshot;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>任务 = 一条长期执行线程：控制状态、开始输入、过程记录与全部执行。
/// 流程语义由 <see cref="IFlowFramework"/> 提供，可变成只经内部方法改动，宿主读 <see cref="Snapshot"/>。</summary>
public sealed class WorkTask : ITaskFlowContext
{
    private readonly List<LoopExecution> _executions = [];
    private readonly DateTimeOffset _createdAt;
    private readonly ProcessObserver? _observer;
    private readonly IFlowState _state;
    private string _title;
    private DateTimeOffset _lastActivityAt;
    private TaskLifecycle _lifecycle = TaskLifecycle.Active;
    private int _controlVersion;
    private bool _completed;
    private readonly IFlowDefinitionRecord? _flowSnapshot;

    /// <summary>建立一个尚未启动的新任务。</summary>
    public WorkTask(
        TaskRegistry registry,
        TaskId id,
        StartInputs inputs,
        FlowTexts startTexts,
        string title,
        IFlowFramework framework,
        IFlowState state,
        IFlowDefinitionRecord? definition,
        ProcessObserver? observer)
    {
        Registry = registry;
        Id = id;
        Inputs = inputs;
        StartTexts = startTexts;
        Framework = framework;
        _state = state;
        _flowSnapshot = definition;
        _createdAt = DateTimeOffset.UtcNow;
        _lastActivityAt = _createdAt;
        _observer = observer;
        _title = title;
        framework.Activate(this);
    }

    private WorkTask(
        TaskRegistry registry,
        TaskRecord record,
        IFlowFramework framework,
        IFlowState state,
        FlowTexts startTexts,
        IReadOnlyList<LoopExecution> executions,
        ProcessObserver? observer)
    {
        Registry = registry;
        Id = record.Identity.Id;
        Inputs = state.Inputs;
        StartTexts = startTexts;
        Framework = framework;
        _state = state;
        _flowSnapshot = record.Flow.Definition;
        _createdAt = record.Identity.CreatedAt;
        _lastActivityAt = record.Identity.LastActivityAt;
        _observer = observer;
        _title = record.Identity.Title;
        _lifecycle = record.Lifecycle;
        _completed = record.Completed;
        Journal.Restore(record.Journal.Entries, record.Journal.DroppedEntries);
        framework.Activate(this);

        foreach (LoopExecution execution in executions)
        {
            AttachExecution(execution);
        }

        Journal.AppendInfo("从存档恢复。");
    }

    /// <summary>从存档恢复任务。执行列表由具体流程实现重建。</summary>
    public static WorkTask Restore(
        TaskRegistry registry,
        TaskRecord record,
        IFlowFramework framework,
        IFlowState state,
        FlowTexts startTexts,
        IReadOnlyList<LoopExecution> executions,
        ProcessObserver? observer = null) =>
        new(registry, record, framework, state, startTexts, executions, observer);

    /// <summary>任务标识。</summary>
    public TaskId Id { get; }

    /// <summary>任务创建时刻。</summary>
    public DateTimeOffset CreatedAt => _createdAt;

    /// <summary>最近活动时刻。</summary>
    internal DateTimeOffset LastActivityAt => _lastActivityAt;

    /// <summary>提交时锁定的流程定义，写存档时使用。</summary>
    internal IFlowDefinitionRecord? FlowSnapshot => _flowSnapshot;

    /// <summary>流程运行状态，由具体流程实现解释。</summary>
    internal IFlowState FlowState => _state;

    /// <summary>任务所属的流程框架。</summary>
    internal IFlowFramework Framework { get; }

    /// <summary>任务启动后的流程控制实例，命令由运行期引擎转发给它。</summary>
    public IFlowControl? Control { get; internal set; }

    /// <summary>任务推进循环已退出，任务真正走完。瞬态的 Done 快照不代表此值为真。</summary>
    public bool Completed
    {
        get
        {
            lock (Gate)
            {
                return _completed;
            }
        }
    }
/// <summary>标记任务已走完。要求持有 <see cref="Gate"/>。</summary>
    public void MarkCompleted()
    {
        _completed = true;
        Journal.AppendInfo("任务完成。");
    }

    /// <summary>通知任务状态已变，存档捕获与观察方据此读取新状态。</summary>
    public void NotifyChanged() => Registry.ReportChanged(Id);

    /// <summary>任务所属的任务域。</summary>
    internal TaskRegistry Registry { get; }

    /// <summary>提交时按开始端口给出的输入。</summary>
    public StartInputs Inputs { get; }

    /// <summary>任务启动时的开始端口文本。</summary>
    public FlowTexts StartTexts { get; }

    /// <summary>在任务状态锁下执行一段操作。状态改动都要经它串行。</summary>
    public T UnderLock<T>(Func<T> action)
    {
        lock (Gate)
        {
            return action();
        }
    }

    /// <summary>任务的过程记录。</summary>
    internal Journal Journal { get; } = new();

    /// <summary>任务内状态改动的串行点。锁序以任务 Gate 为最外层，注册表先于执行索引。</summary>
    public Lock Gate { get; } = new();

    private event Action<LoopExecution>? ExecutionSettled;

    /// <summary>执行收口后的通知，回答等待方借此取回本轮产出。要求持有 Gate 触发。</summary>
    public void NotifyExecutionSettled(LoopExecution execution) => ExecutionSettled?.Invoke(execution);

    /// <summary>订阅执行收口通知，返回解除订阅的委托。</summary>
    public Action SubscribeExecutionSettled(Action<LoopExecution> handler)
    {
        ExecutionSettled += handler;

        return () => ExecutionSettled -= handler;
    }

    /// <summary>任务的全部执行，按提交顺序。</summary>
    public IReadOnlyList<LoopExecution> Executions => _executions;

    /// <summary>任务标题，创建时按输入与流程名推导，可由宿主改写。</summary>
    public string Title
    {
        get
        {
            lock (Gate)
            {
                return _title;
            }
        }

        internal set
        {
            lock (Gate)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _title = TaskTitle.Shorten(value);
                    NotifyChanged();
                }
            }
        }
    }

    /// <summary>任务的整体状态，由控制状态、步骤状态与在跑的执行汇总得出。</summary>
    public TaskState State
    {
        get
        {
            lock (Gate)
            {
                return Summarize();
            }
        }
    }

    /// <summary>任务的控制状态。</summary>
    public TaskLifecycle Lifecycle
    {
        get
        {
            lock (Gate)
            {
                return _lifecycle;
            }
        }
    }

    /// <summary>要求持有 <see cref="Gate"/>。控制状态优先于执行与步骤状态。</summary>
    private TaskState Summarize()
    {
        if (_lifecycle == TaskLifecycle.Terminated)
        {
            return TaskState.Terminated;
        }

        if (_lifecycle == TaskLifecycle.Paused)
        {
            return TaskState.Paused;
        }

        if (_completed)
        {
            return TaskState.Done;
        }

        if (_executions.Any(execution => execution.IsLive))
        {
            return TaskState.Running;
        }

        if (Control?.HasBlocked == true)
        {
            return TaskState.Blocked;
        }

        return Control?.HasWork == true ? TaskState.Running : TaskState.Done;
    }

    /// <summary>任务控制代次，暂停、恢复与收口暂停都会推进。</summary>
    public int ControlVersion => _controlVersion;

    /// <summary>任务域是否在可恢复的中断收口时暂停任务。</summary>
    internal bool PauseOnRestore => Registry.PauseOnRestore;

    /// <summary>任务域是否在可恢复的中断收口时暂停任务。</summary>
    bool ITaskFlowContext.PauseOnRestore => PauseOnRestore;

    /// <summary>任务的过程记录。</summary>
    public IReadOnlyList<JournalEntry> JournalEntries => Journal.Entries;

    /// <summary>存档恢复时丢弃的过程记录条数。</summary>
    public int DroppedJournal => Journal.DroppedEntries;

    /// <summary>用存档内容覆盖过程记录。</summary>
    public void RestoreJournal(IReadOnlyList<JournalEntry> entries, int dropped) =>
        Journal.Restore(entries, dropped);

    /// <summary>追加一条信息记录。</summary>
    public void AppendInfo(string text) => Journal.AppendInfo(text);

    /// <summary>追加一条错误记录。</summary>
    public void AppendError(string text) => Journal.AppendError(text);

    /// <summary>追加一条过程记录。</summary>
    public void AppendJournal(JournalLevel level, string text)
    {
        if (level == JournalLevel.Error)
        {
            Journal.AppendError(text);
        }
        else
        {
            Journal.AppendInfo(text);
        }
    }

    /// <summary>上报一条执行事件。</summary>
    public void Report(ExecutionNotice notice) => Registry.Report(notice);

    /// <summary>可恢复的中断收口后暂停任务，只停止新的调度，不中断在跑的执行。</summary>
    public void PauseForRestore()
    {
        if (_completed || _lifecycle != TaskLifecycle.Active)
        {
            return;
        }

        _lifecycle = TaskLifecycle.Paused;
        _controlVersion++;
        _lastActivityAt = DateTimeOffset.UtcNow;
        Journal.AppendInfo("可恢复的中断已收口，任务暂停等待恢复。");
        NotifyChanged();
    }

    /// <summary>暂停任务：不再启动新的执行，等待中的在跑执行由推进器中断。</summary>
    public ErrorOr<Success> Pause()
    {
        if (_lifecycle == TaskLifecycle.Terminated)
        {
            return [TaskErrors.Terminated(Id)];
        }

        if (_lifecycle == TaskLifecycle.Paused)
        {
            return [TaskErrors.AlreadyPaused(Id)];
        }

        if (_completed)
        {
            return [TaskErrors.DoneCannotPause(Id)];
        }

        _lifecycle = TaskLifecycle.Paused;
        _controlVersion++;
        _lastActivityAt = DateTimeOffset.UtcNow;
        Journal.AppendInfo("任务暂停。");
        NotifyChanged();

        return Result.Success;
    }

    /// <summary>恢复暂停的任务，之后由推进器重新取待处理步骤。</summary>
    public ErrorOr<Success> Resume()
    {
        if (_lifecycle == TaskLifecycle.Terminated)
        {
            return [TaskErrors.Terminated(Id)];
        }

        if (_lifecycle == TaskLifecycle.Active)
        {
            return [TaskErrors.NotPaused(Id)];
        }

        _lifecycle = TaskLifecycle.Active;
        _controlVersion++;
        _lastActivityAt = DateTimeOffset.UtcNow;
        Journal.AppendInfo("任务恢复推进。");
        NotifyChanged();

        return Result.Success;
    }
/// <summary>终止任务：在跑的执行一并中断。要求持有 <see cref="Gate"/>。</summary>
    public ErrorOr<Success> Terminate()
    {
        if (_lifecycle == TaskLifecycle.Terminated)
        {
            return [TaskErrors.Terminated(Id)];
        }

        if (_completed)
        {
            return [TaskErrors.DoneCannotTerminate(Id)];
        }

        _lifecycle = TaskLifecycle.Terminated;
        Journal.AppendInfo("任务终止。");

        foreach (LoopExecution execution in _executions.Where(execution => execution.IsLive))
        {
            execution.Interrupt("任务终止。");
        }

        _lastActivityAt = DateTimeOffset.UtcNow;
        NotifyChanged();

        return Result.Success;
    }

    /// <summary>记下一个执行。要求持有 <see cref="Gate"/>。</summary>
    public void AttachExecution(LoopExecution execution)
    {
        _executions.Add(execution);
        _lastActivityAt = DateTimeOffset.UtcNow;
        execution.Started += () => OnExecutionStarted(execution);
        Journal.AppendInfo($"执行 {execution.Id.Value} 创建。");
        if (_observer?.Content is { } onContent)
        {
            execution.ContentEmitted += onContent;
        }

        if (_observer?.Entry is { } onEntry)
        {
            execution.EntryEmitted += onEntry;
        }

        execution.Changed += NotifyChanged;
        ExecutionAttached?.Invoke(execution);
    }

    /// <summary>执行真正进入运行态时记一次执行尝试。</summary>
    private void OnExecutionStarted(LoopExecution execution)
    {
        lock (Gate)
        {
            _lastActivityAt = DateTimeOffset.UtcNow;
            Journal.AppendInfo($"执行 {execution.Id.Value} 开始。");
            NotifyChanged();
        }
    }

    private event Action<LoopExecution>? ExecutionAttached;

    /// <summary>订阅新执行启动的通知，回答等待方借此挂上流式观察。要求持有 Gate 触发。</summary>
    public Action SubscribeExecutionAttached(Action<LoopExecution> handler)
    {
        ExecutionAttached += handler;

        return () => ExecutionAttached -= handler;
    }

    /// <summary>记一次任务活动。</summary>
    public void Touch() => _lastActivityAt = DateTimeOffset.UtcNow;

    /// <summary>当前状态的只读快照，含流程视图、过程记录与执行列表。</summary>
    public TaskSnapshot Snapshot()
    {
        lock (Gate)
        {
            return Framework.CreateSnapshot(this);
        }
    }

    /// <summary>捕获任务存档。要求持有 <see cref="Gate"/>。</summary>
    internal TaskRecord CaptureRecord()
    {
        FlowRecord flow = Framework.CaptureRecord(this);

        return new TaskRecord(
            new TaskIdentity(Id, Title, _createdAt, _lastActivityAt),
            DateTimeOffset.UtcNow,
            _lifecycle,
            _completed,
            new TaskJournalRecord(Journal.Entries, Journal.DroppedEntries),
            flow,
            Framework.CaptureExecutions(this));
    }
}

/// <summary>流程框架：一个流程种类的提交、恢复、控制与存档实现。</summary>
public interface IFlowFramework
{
    /// <summary>流程记录种类。</summary>
    string Kind { get; }

    /// <summary>流程展示名。</summary>
    string Name { get; }

    /// <summary>校验提交输入并给出初始状态、标题与流程定义记录。</summary>
    ErrorOr<FlowSubmission> Prepare(TaskId id, FlowName name, StartInputs inputs, string? title);

    /// <summary>把任务上下文接到流程状态。</summary>
    void Activate(WorkTask task);

    /// <summary>是否负责该流程名。</summary>
    bool Handles(FlowName name);

    /// <summary>为任务建立控制实例。</summary>
    IFlowControl CreateControl(WorkTask task);

    /// <summary>当前流程视图，控制实例不存在时使用。</summary>
    IFlowView CaptureView(WorkTask task);

    /// <summary>任务快照，含流程视图、流程投影与中性执行列表。</summary>
    TaskSnapshot CreateSnapshot(WorkTask task);

    /// <summary>捕获流程记录。要求持有任务 Gate。</summary>
    FlowRecord CaptureRecord(WorkTask task);

    /// <summary>捕获流程执行记录。要求持有任务 Gate。</summary>
    IReadOnlyList<ExecutionRecord> CaptureExecutions(WorkTask task);

    /// <summary>从存档恢复流程状态与执行。</summary>
    ErrorOr<FlowRestore> Restore(TaskRecord record);
}

/// <summary>一次提交准备好的流程状态。</summary>
public sealed record FlowSubmission(
    IFlowState State,
    string Title,
    FlowTexts StartTexts,
    IFlowDefinitionRecord? Definition);

/// <summary>从存档恢复的流程状态。</summary>
public sealed record FlowRestore(
    IFlowState State,
    FlowTexts StartTexts,
    IReadOnlyList<LoopExecution> Executions,
    FlowSeed Seed);

/// <summary>流程运行状态的标记。由具体流程实现解释。</summary>
public interface IFlowState
{
    /// <summary>提交时的开始输入。</summary>
    StartInputs Inputs { get; }
}

/// <summary>按种类查流程框架。</summary>
public interface IFlowFrameworkSource
{
    /// <summary>全部已注册的流程框架。</summary>
    IReadOnlyList<IFlowFramework> All();

    /// <summary>按种类取流程框架，不存在时返回错误。</summary>
    ErrorOr<IFlowFramework> Find(string kind);

    /// <summary>按流程名取负责的框架，不存在时返回错误。</summary>
    ErrorOr<IFlowFramework> Resolve(FlowName name);
}

/// <summary>流程控制建立与登记执行的中性端口。</summary>
public interface IFlowExecutionGateway
{
    /// <summary>为任务建立一个排队中的执行并分配执行号。</summary>
    LoopExecution Open(WorkTask owner, ExecutionRequest request);

    /// <summary>按执行号取执行，不存在时返回错误。</summary>
    ErrorOr<LoopExecution> Find(ExecutionId id);

    /// <summary>为一次执行附加流程实现自己的执行对象。</summary>
    void Attach(LoopExecution execution, object payload);

    /// <summary>按执行号停止一个已启动的执行。</summary>
    ErrorOr<Success> Stop(WorkTask owner, ExecutionId id);
}