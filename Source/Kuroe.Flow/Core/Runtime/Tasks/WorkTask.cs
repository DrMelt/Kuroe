using ErrorOr;
using Kuroe.Flow.Runtime.Process;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Shared;
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
public sealed class WorkTask : INodeTask
{
    private readonly List<Execution> _executions = [];
    private readonly DateTimeOffset _createdAt;
    private readonly Action<string>? _onText;
    private readonly Action<JournalEntry>? _onEntry;
    private string _title;
    private DateTimeOffset _lastActivityAt;
    private TaskLifecycle _lifecycle = TaskLifecycle.Active;
    private bool _completed;

    internal WorkTask(
        TaskRegistry registry,
        TaskId id,
        StartInputs inputs,
        IPortTexts startTexts,
        StaticPortValues staticPorts,
        FlowDefinition flow,
        INodeGraph graph,
        string title,
        Action<string>? onText,
        Action<JournalEntry>? onEntry)
    {
        Registry = registry;
        Id = id;
        Inputs = inputs;
        StartTexts = startTexts;
        StaticPorts = staticPorts;
        Flow = flow;
        Graph = graph;
        Runtime = new TaskRuntime(this);
        _createdAt = DateTimeOffset.UtcNow;
        _lastActivityAt = _createdAt;
        _onText = onText;
        _onEntry = onEntry;
        _title = title;
    }

    private WorkTask(
        TaskRegistry registry,
        TaskId id,
        StartInputs inputs,
        IPortTexts startTexts,
        StaticPortValues staticPorts,
        FlowDefinition flow,
        INodeGraph graph,
        string title,
        DateTimeOffset createdAt,
        DateTimeOffset lastActivityAt,
        Action<string>? onText,
        Action<JournalEntry>? onEntry)
    {
        Registry = registry;
        Id = id;
        Inputs = inputs;
        StartTexts = startTexts;
        StaticPorts = staticPorts;
        Flow = flow;
        Graph = graph;
        Runtime = new TaskRuntime(this);
        _createdAt = createdAt;
        _lastActivityAt = lastActivityAt;
        _onText = onText;
        _onEntry = onEntry;
        _title = title;
    }

    /// <summary>从检查点恢复任务。未完成任务恢复为暂停，完成的任务保持完成，终止的任务保持终止。</summary>
    internal static WorkTask Restore(
        TaskRegistry registry,
        TaskCheckpoint checkpoint,
        CompiledFlow flow,
        IPortTexts startTexts,
        StaticPortValues staticPorts,
        IReadOnlyList<Execution> executions,
        Action<string>? onText = null,
        Action<JournalEntry>? onEntry = null)
    {
        WorkTask task = new(
            registry,
            checkpoint.Identity.Id,
            checkpoint.Start.Inputs,
            startTexts,
            staticPorts,
            flow.Model,
            flow.Graph,
            checkpoint.Identity.Title,
            checkpoint.Identity.CreatedAt,
            checkpoint.Identity.LastActivityAt,
            onText,
            onEntry);

        task.Journal.Restore(checkpoint.Journal.Entries, checkpoint.Journal.DroppedEntries);
        task.FlowSnapshot = checkpoint.Flow;
        task._executions.AddRange(executions);
        task.Runtime.Restore(checkpoint.Nodes);
        foreach (NodePath node in executions
            .Where(execution => execution.State == ExecutionState.Interrupted)
            .Select(execution => execution.Input.Node))
        {
            task.Runtime.Executable(node).EnterBlocked([node]);
        }

        task._lifecycle = task.DetermineRestoredLifecycle(checkpoint);

        return task;
    }

    /// <summary>终止保持终止，已完成保持完成，其余未完成任务恢复为暂停。</summary>
    private TaskLifecycle DetermineRestoredLifecycle(TaskCheckpoint checkpoint)
    {
        if (checkpoint.Lifecycle == TaskLifecycle.Terminated)
        {
            return TaskLifecycle.Terminated;
        }

        _completed = checkpoint.Completed;

        return _completed ? TaskLifecycle.Active : TaskLifecycle.Paused;
    }

    /// <summary>任务标识。</summary>
    public TaskId Id { get; }

    /// <summary>任务创建时刻。</summary>
    public DateTimeOffset CreatedAt => _createdAt;

    /// <summary>最近活动时刻。</summary>
    internal DateTimeOffset LastActivityAt => _lastActivityAt;

    /// <summary>提交时锁定的流程快照，写检查点时使用。</summary>
    internal FlowSnapshot? FlowSnapshot { get; set; }

    /// <summary>推进循环给出的待处理目标，写检查点时使用。</summary>
    internal Func<SchedulerSeed>? SeedSource { get; set; }

    /// <summary>捕获待处理目标，没有推进循环时为空。</summary>
    internal SchedulerSeed CaptureSeed() => SeedSource?.Invoke() ?? new SchedulerSeed([]);

    /// <summary>任务是否已走完，由推进循环退出时置位。</summary>
    internal bool Completed
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
    internal void MarkCompleted() => _completed = true;

    /// <summary>通知任务已走完，写检查点由任务域决定。</summary>
    internal void NotifyCompleted() => Registry.ReportCompleted(this);

    /// <summary>任务所属的任务域，域内任务号与任务列表的范围由它给出。</summary>
    internal TaskRegistry Registry { get; }

    /// <summary>提交时按开始端口给出的输入。</summary>
    public StartInputs Inputs { get; }

    /// <summary>任务启动时注入的开始端口文本，绑定到宿主输入名的端口按它取值。</summary>
    internal IPortTexts StartTexts { get; }

    /// <summary>提交时预解析的静态端口文本，任务内全部静态读取共用。</summary>
    internal StaticPortValues StaticPorts { get; }

    /// <summary>提交时锁定的流程模板。</summary>
    public FlowDefinition Flow { get; }

    /// <summary>提交时锁定的流程执行视图。</summary>
    internal INodeGraph Graph { get; }

    /// <summary>任务的流程编译视图，节点实现经它定位成员与读端口类型。</summary>
    INodeGraph INodeTask.Graph => Graph;

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

    private event Action<Execution>? ExecutionSettled;

    /// <summary>执行收口后的通知，回答等待方借此取回本轮产出。要求持有 Gate 触发。</summary>
    internal void NotifyExecutionSettled(Execution execution) => ExecutionSettled?.Invoke(execution);

    /// <summary>订阅执行收口通知，返回解除订阅的委托。</summary>
    public Action SubscribeExecutionSettled(Action<Execution> handler)
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
                }
            }
        }
    }

    /// <summary>提交顺序排列的执行，宿主对话功能读数用。仅在持有 <see cref="Gate"/> 时读写。</summary>
    public IReadOnlyList<Execution> Executions => _executions;

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

        if (Summarize() == TaskState.Done)
        {
            return [TaskErrors.DoneCannotPause(Id)];
        }

        _lifecycle = TaskLifecycle.Paused;
        _lastActivityAt = DateTimeOffset.UtcNow;

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
        _lastActivityAt = DateTimeOffset.UtcNow;

        return Result.Success;
    }

    /// <summary>终止任务：在跑的执行与执行节点状态一并终止。要求持有 <see cref="Gate"/>。</summary>
    internal ErrorOr<Success> Terminate()
    {
        if (_lifecycle == TaskLifecycle.Terminated)
        {
            return [TaskErrors.Terminated(Id)];
        }

        if (Summarize() == TaskState.Done)
        {
            return [TaskErrors.DoneCannotTerminate(Id)];
        }

        _lifecycle = TaskLifecycle.Terminated;
        foreach (Execution execution in _executions.Where(execution => execution.IsLive))
        {
            execution.Cancel();
        }

        Runtime.Cancel();
        _lastActivityAt = DateTimeOffset.UtcNow;

        return Result.Success;
    }

    /// <summary>记下一个启动的执行。要求持有 <see cref="Gate"/>。</summary>
    internal void Attach(Execution execution)
    {
        _executions.Add(execution);
        Runtime.Executable(execution.Input.Node).RecordExecution();
        _lastActivityAt = DateTimeOffset.UtcNow;
        if (_onText is { } onText)
        {
            execution.TextEmitted += onText;
        }

        if (_onEntry is { } onEntry)
        {
            execution.EntryEmitted += onEntry;
        }

        ExecutionAttached?.Invoke(execution);
    }

    private event Action<Execution>? ExecutionAttached;

    /// <summary>订阅新执行启动的通知，回答等待方借此挂上流式观察。要求持有 Gate 触发。</summary>
    public Action SubscribeExecutionAttached(Action<Execution> handler)
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
            Dictionary<ExecutionId, ExecutionSnapshot> executions = _executions.ToDictionary(execution => execution.Id, execution => execution.Snapshot());

            List<ExecutableSnapshot> executables =
            [
                .. Graph.ExecutableNodes.Select(node => new ExecutableSnapshot(
                    node,
                    Runtime.StaticPorts.For(node),
                    Runtime.StaticPorts.ConstantsFor(node),
                    [.. _executions.Where(execution => execution.Input.Node == node.Path).Select(execution => executions[execution.Id])])),
            ];

            TaskProjection projection = new(Graph, Runtime.Materializer, Runtime.StaticPorts);
            List<ExecutableStateSnapshot> executableStates = [.. Graph.ExecutableNodes.Select(node => projection.State(node.Path))];

            return new TaskSnapshot(Id, _title, Inputs, StartTexts, Flow, Graph, Summarize(),
                _executions.Count(execution => execution.IsLive), executables, executableStates,
                projection.Containers(), Journal.Entries, Journal.DroppedEntries, _createdAt, _lastActivityAt);
        }
    }
}
