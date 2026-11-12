using ErrorOr;
using Kuroe.Flow.Runtime.Process;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Tasks.Records;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Runtime.Graph;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>任务 = 一条长期执行线程：按流程推进的执行节点状态与启动的全部执行。
/// 可变成只经内部方法改动，宿主读 <see cref="Snapshot"/>。</summary>
public sealed class WorkTask
{
    private readonly List<GraphExecution> _executions = [];
    private readonly DateTimeOffset _createdAt;
    private readonly ProcessObserver? _observer;
    private string _title;
    private DateTimeOffset _lastActivityAt;
    private TaskLifecycle _lifecycle = TaskLifecycle.Active;
    private int _controlVersion;
    private bool _completed;

    internal WorkTask(
        TaskRegistry registry,
        TaskId id,
        StartInputs inputs,
        IPortTexts startTexts,
        FlowDefinition flow,
        NodeGraph graph,
        string title,
        ProcessObserver? observer)
    {
        Registry = registry;
        Id = id;
        Inputs = inputs;
        StartTexts = startTexts;
        StartPorts = new StartPortValues(graph.StartPorts, startTexts, inputs);
        Flow = flow;
        Graph = graph;
        Runtime = new TaskRuntime(this);
        _createdAt = DateTimeOffset.UtcNow;
        _lastActivityAt = _createdAt;
        _observer = observer;
        _title = title;
    }

    private WorkTask(
        TaskRegistry registry,
        TaskId id,
        StartInputs inputs,
        IPortTexts startTexts,
        FlowDefinition flow,
        NodeGraph graph,
        string title,
        DateTimeOffset createdAt,
        DateTimeOffset lastActivityAt,
        ProcessObserver? observer)
    {
        Registry = registry;
        Id = id;
        Inputs = inputs;
        StartTexts = startTexts;
        StartPorts = new StartPortValues(graph.StartPorts, startTexts, inputs);
        Flow = flow;
        Graph = graph;
        Runtime = new TaskRuntime(this);
        _createdAt = createdAt;
        _lastActivityAt = lastActivityAt;
        _observer = observer;
        _title = title;
    }

    /// <summary>从存档恢复任务。未完成任务恢复为暂停，完成的任务保持完成，终止的任务保持终止且节点恢复为取消态。</summary>
    internal static WorkTask Restore(
        TaskRegistry registry,
        TaskRecord record,
        GraphFlowStateRecord state,
        CompiledFlow flow,
        IPortTexts startTexts,
        IReadOnlyList<GraphExecution> executions,
        ProcessObserver? observer = null)
    {
        WorkTask task = new(
            registry,
            record.Identity.Id,
            state.Start.Inputs,
            startTexts,
            flow.Model,
            flow.Graph,
            record.Identity.Title,
            record.Identity.CreatedAt,
            record.Identity.LastActivityAt,
            observer);

        task.Journal.Restore(record.Journal.Entries, record.Journal.DroppedEntries);
        task.FlowSnapshot = record.Flow.Definition as GraphFlowDefinitionRecord
            ?? throw new ArgumentException("存档的流程定义不是图流程。");
        task._executions.AddRange(executions);
        task.Runtime.Restore(state.Nodes, state.Ports);
        task._lifecycle = task.DetermineRestoredLifecycle(record);
        if (task._lifecycle == TaskLifecycle.Terminated)
        {
            task.Runtime.Cancel();
        }
        else
        {
            foreach (GraphExecution execution in executions
                .Where(execution => execution.State == ExecutionState.Interrupted))
            {
                GraphExecutionSnapshot snapshot = execution.Snapshot();
                var runtime = task.Runtime.Executable(execution.Input.Node);
                if (snapshot.StartedAt is null)
                {
                    if (runtime.Block?.Execution == execution.Id)
                    {
                        runtime.ClearBlock();
                    }

                    runtime.RequestRerun();
                    continue;
                }

                if (runtime.Block is null)
                {
                    runtime.EnterBlocked(
                        [runtime.Path],
                        new NodeBlock("进程退出前未收口。", execution.Id, record.SavedAt));
                }
            }
        }

        if (registry.Restorers is not null)
        {
            task.Runtime.RefreshRestorePlans();
        }

        task.Journal.AppendInfo("从存档恢复。");

        return task;
    }

    /// <summary>终止保持终止，已完成保持完成，其余未完成任务恢复为暂停。</summary>
    private TaskLifecycle DetermineRestoredLifecycle(TaskRecord record)
    {
        if (record.Lifecycle == TaskLifecycle.Terminated)
        {
            return TaskLifecycle.Terminated;
        }

        _completed = record.Completed;

        return _completed ? TaskLifecycle.Active : TaskLifecycle.Paused;
    }

    /// <summary>任务标识。</summary>
    public TaskId Id { get; }

    /// <summary>任务创建时刻。</summary>
    public DateTimeOffset CreatedAt => _createdAt;

    /// <summary>最近活动时刻。</summary>
    internal DateTimeOffset LastActivityAt => _lastActivityAt;

    /// <summary>提交时锁定的流程快照，写存档时使用。</summary>
    internal GraphFlowDefinitionRecord? FlowSnapshot { get; set; }

    /// <summary>推进循环给出的待处理目标，写存档时使用。</summary>
    internal Func<SchedulerSeed>? SeedSource { get; set; }

    /// <summary>捕获待处理目标，没有推进循环时为空。</summary>
    internal SchedulerSeed CaptureSeed() => SeedSource?.Invoke() ?? new SchedulerSeed([]);

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
    internal void MarkCompleted()
    {
        _completed = true;
        Runtime.ClearRestorePlans();
        Journal.AppendInfo("任务完成。");
    }

    /// <summary>通知任务状态已变，存档捕获与观察方据此读取新状态。</summary>
    internal void NotifyChanged() => Registry.ReportChanged(Id);

    /// <summary>任务所属的任务域，域内任务号与任务列表的范围由它给出。</summary>
    internal TaskRegistry Registry { get; }

    /// <summary>提交时按开始端口给出的输入。</summary>
    public StartInputs Inputs { get; }

    /// <summary>任务启动时由宿主给出的开始端口文本，宿主开始输入表里的端口按它取值。</summary>
    internal IPortTexts StartTexts { get; }

    /// <summary>任务启动时确定的开始端口值，固定值、宿主给值与提交输入按端口解析。</summary>
    internal IPortTexts StartPorts { get; }

    /// <summary>提交时锁定的流程模板。</summary>
    public FlowDefinition Flow { get; }

    /// <summary>提交时锁定的流程执行视图。</summary>
    public NodeGraph Graph { get; }

    /// <summary>任务的节点上下文，宿主扩展按节点路径取节点运行期对象时使用。</summary>
    public INodeContext Context => Runtime;

    /// <summary>在任务状态锁下执行一段操作。状态改动都要经它串行。</summary>
    public T UnderLock<T>(Func<T> action)
    {
        lock (Gate)
        {
            return action();
        }
    }

    /// <summary>任务的过程记录，只记执行侧诊断内容。</summary>
    internal Journal Journal { get; } = new();

    /// <summary>任务内状态改动的串行点。锁序以任务 Gate 为最外层，注册表先于执行索引。</summary>
    internal Lock Gate { get; } = new();

    private event Action<GraphExecution>? ExecutionSettled;

    /// <summary>执行收口后的通知，回答等待方借此取回本轮产出。要求持有 Gate 触发。</summary>
    internal void NotifyExecutionSettled(GraphExecution execution) => ExecutionSettled?.Invoke(execution);

    /// <summary>订阅执行收口通知，返回解除订阅的委托。</summary>
    public Action SubscribeExecutionSettled(Action<GraphExecution> handler)
    {
        ExecutionSettled += handler;

        return () => ExecutionSettled -= handler;
    }

    /// <summary>图驱动的执行状态，执行节点就地决定激活与发布。</summary>
    internal TaskRuntime Runtime { get; }

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

    /// <summary>提交顺序排列的执行，宿主对话功能读数用。仅在持有 <see cref="Gate"/> 时读写。</summary>
    public IReadOnlyList<GraphExecution> Executions => _executions;

    /// <summary>任务的整体状态，由控制状态、执行节点状态与在跑的执行汇总得出。</summary>
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

    /// <summary>任务的控制状态，暂停与终止只经内部状态迁移改动。</summary>
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

    /// <summary>要求持有 <see cref="Gate"/>。控制状态优先于执行与节点状态。</summary>
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

        if (Runtime.HasBlocked)
        {
            return TaskState.Blocked;
        }

        return Runtime.HasWork ? TaskState.Running : TaskState.Done;
    }

    /// <summary>任务控制代次，暂停、恢复与收口暂停都会推进。要求持有 Gate 读取。</summary>
    internal int ControlVersion => _controlVersion;

    /// <summary>任务域是否在可恢复的中断收口时暂停任务。</summary>
    internal bool PauseOnRestore => Registry.PauseOnRestore;

    /// <summary>可恢复的中断收口后暂停任务，只停止新的调度，不中断在跑的执行。要求持有 <see cref="Gate"/>。</summary>
    internal void PauseForRestore()
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

    /// <summary>暂停任务：不再启动新的执行，等待中的在跑执行由推进器中断。要求持有 <see cref="Gate"/>。</summary>
    internal ErrorOr<Success> Pause()
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

    /// <summary>恢复暂停的任务，之后由推进器重新取待处理目标。要求持有 <see cref="Gate"/>。</summary>
    internal ErrorOr<Success> Resume()
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

    /// <summary>终止任务：在跑的执行与执行节点状态一并终止。要求持有 <see cref="Gate"/>。</summary>
    internal ErrorOr<Success> Terminate()
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
        Runtime.ClearRestorePlans();
        Journal.AppendInfo("任务终止。");

        foreach (GraphExecution execution in _executions.Where(execution => execution.IsLive))
        {
            execution.Interrupt("任务终止。");
        }

        Runtime.Cancel();
        _lastActivityAt = DateTimeOffset.UtcNow;
        NotifyChanged();

        return Result.Success;
    }

    /// <summary>记下一个启动的执行。要求持有 <see cref="Gate"/>。</summary>
    internal void Attach(GraphExecution execution)
    {
        _executions.Add(execution);
        _lastActivityAt = DateTimeOffset.UtcNow;
        execution.RestoreDataChanged += OnRestoreDataChanged;
        execution.Started += () => OnExecutionStarted(execution);
        Journal.AppendInfo($"执行 {execution.Id.Value} 创建，节点 {execution.Input.Node}。");
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
    private void OnExecutionStarted(GraphExecution execution)
    {
        lock (Gate)
        {
            Runtime.Executable(execution.Input.Node).RecordExecution();
            _lastActivityAt = DateTimeOffset.UtcNow;
            Journal.AppendInfo($"执行 {execution.Id.Value} 开始，节点 {execution.Input.Node}。");
            NotifyChanged();
        }
    }

    private void OnRestoreDataChanged() => NotifyChanged();

    private event Action<GraphExecution>? ExecutionAttached;

    /// <summary>订阅新执行启动的通知，回答等待方借此挂上流式观察。要求持有 Gate 触发。</summary>
    public Action SubscribeExecutionAttached(Action<GraphExecution> handler)
    {
        ExecutionAttached += handler;

        return () => ExecutionAttached -= handler;
    }

    internal void Touch() => _lastActivityAt = DateTimeOffset.UtcNow;

    /// <summary>当前状态的只读快照，含执行节点、执行节点状态、执行与过程记录。</summary>
    public TaskSnapshot Snapshot()
    {
        lock (Gate)
        {
            IReadOnlyList<GraphExecutionSnapshot> executions = [.. _executions.Select(execution => execution.Snapshot())];
            TaskProjection projection = new(Graph, Runtime.Materializer, Runtime.StaticPorts);

            return new TaskSnapshot(Id, _title, Inputs, StartTexts, Flow, Graph, Summarize(),
                _executions.Count(execution => execution.IsLive),
                projection.Executables(executions),
                projection.ExecutableStates(),
                projection.Containers(), Journal.Entries, Journal.DroppedEntries, _createdAt, _lastActivityAt);
        }
    }
}
