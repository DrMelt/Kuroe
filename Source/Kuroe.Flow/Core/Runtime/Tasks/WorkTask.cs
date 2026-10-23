using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Process;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Runtime.Graph;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>任务 = 一条长期执行线程：按流程推进的执行节点状态与启动的全部执行。
/// 可变成只经内部方法改动，宿主读 <see cref="Snapshot"/>。</summary>
public sealed class WorkTask : INodeTask
{
    private const int TitleLimit = 40;

    private readonly List<Execution> _executions = [];
    private readonly DateTimeOffset _createdAt;
    private readonly IProcessSink? _observer;
    private string _title;
    private DateTimeOffset _lastActivityAt;
    private bool _canceled;

    internal WorkTask(
        TaskRegistry registry,
        TaskId id,
        string goal,
        FlowDefinition flow,
        INodeGraph graph,
        string? title,
        IProcessSink? observer)
    {
        Registry = registry;
        Id = id;
        Goal = goal;
        Flow = flow;
        Graph = graph;
        Runtime = new TaskRuntime(this);
        _createdAt = DateTimeOffset.UtcNow;
        _lastActivityAt = _createdAt;
        _observer = observer;
        _title = string.IsNullOrWhiteSpace(title) ? Shorten(goal) : Shorten(title);
    }

    /// <summary>任务标识。</summary>
    public TaskId Id { get; }

    /// <summary>任务所属的任务域，域内任务号与任务列表的范围由它给出。</summary>
    internal TaskRegistry Registry { get; }

    /// <summary>提交时给出的目标。</summary>
    public string Goal { get; }

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

    /// <summary>任务标题，未给出时取目标首行。</summary>
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
                    _title = Shorten(value);
                }
            }
        }
    }

    /// <summary>提交顺序排列的执行，宿主对话功能读数用。仅在持有 <see cref="Gate"/> 时读写。</summary>
    public IReadOnlyList<Execution> Executions => _executions;

    /// <summary>任务的整体状态，由取消标记、执行节点状态与在跑的执行汇总得出。</summary>
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
    /// <summary>要求持有 <see cref="Gate"/>。任务状态由取消标记、执行节点状态与在跑的执行汇总得出，不单独维护。</summary>
    private TaskState Summarize()
    {
        if (_canceled)
        {
            return TaskState.Canceled;
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

    /// <summary>任务被取消：在跑的执行与执行节点状态一并终止。要求持有 <see cref="Gate"/>。</summary>
    internal void Cancel()
    {
        _canceled = true;
        foreach (Execution execution in _executions.Where(execution => execution.IsLive))
        {
            execution.Cancel();
        }

        Runtime.Cancel();
        _lastActivityAt = DateTimeOffset.UtcNow;
    }

    /// <summary>记下一个启动的执行。要求持有 <see cref="Gate"/>。</summary>
    internal void Attach(Execution execution)
    {
        _executions.Add(execution);
        Runtime.Executable(execution.Input.Node).RecordExecution();
        _lastActivityAt = DateTimeOffset.UtcNow;
        if (_observer is { } observer)
        {
            execution.Observe(observer);
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
                .. Graph.ExecutableNodes.Select(node => new ExecutableSnapshot(node,
                    [.. _executions.Where(execution => execution.Input.Node == node.Path).Select(execution => executions[execution.Id])])),
            ];

            TaskProjection projection = new(Graph, Runtime.Materializer);
            List<ExecutableStateSnapshot> executableStates = [.. Graph.ExecutableNodes.Select(node => projection.State(node.Path))];

            return new TaskSnapshot(Id, _title, Goal, Flow, Graph, Summarize(),
                _executions.Count(execution => execution.IsLive), executables, executableStates,
                projection.Containers(), Journal.Entries, Journal.DroppedEntries, _createdAt, _lastActivityAt);
        }
    }

    /// <summary>取目标的首行作标题，过长时截断。</summary>
    private static string Shorten(string text)
    {
        string first = text.Trim().Split('\n')[0].Trim();

        return first.Length <= TitleLimit ? first : string.Concat(first.AsSpan(0, TitleLimit), "…");
    }
}
