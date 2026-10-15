using Kuroe.Flow.Execution.Runs;
using Kuroe.Flow.Execution.Turns;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Runs;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.RuntimeGraph;
using Kuroe.Flow.StaticGraph;

namespace Kuroe.Flow.Execution.Tasks;

/// <summary>任务 = 一条长期执行线程：按流程推进的执行节点状态与启动的全部 run。
/// 可变成只经内部方法改动，宿主读 <see cref="Snapshot"/>。</summary>
public sealed class WorkTask : INodeTask
{
    private const int TitleLimit = 40;

    private readonly List<Run> _runs = [];
    private string _title;
    private DateTimeOffset _lastActivityAt;
    private bool _canceled;

    internal WorkTask(TaskId id, string goal, FlowDefinition flow, NodeGraph graph, string? title)
    {
        Id = id;
        Goal = goal;
        Flow = flow;
        Graph = graph;
        Journal = new TurnJournal();
        Runtime = new TaskRuntime(this);
        _lastActivityAt = DateTimeOffset.UtcNow;
        _title = string.IsNullOrWhiteSpace(title) ? Shorten(goal) : title;
    }

    /// <summary>任务标识。</summary>
    public TaskId Id { get; }

    /// <summary>提交时给出的目标。</summary>
    public string Goal { get; }

    /// <summary>提交时锁定的流程模板。</summary>
    public FlowDefinition Flow { get; }

    /// <summary>提交时锁定的流程编译视图。</summary>
    internal NodeGraph Graph { get; }

    /// <summary>任务的流程编译视图，节点实现经它定位成员与读端口类型。</summary>
    INodeGraph INodeTask.Graph => Graph;

    /// <summary>任务的节点上下文，宿主扩展按序号取节点运行期对象时使用。</summary>
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
    internal TurnJournal Journal { get; }

    /// <summary>任务内状态改动的串行点。锁序固定为任务 Gate、注册表、派发器。</summary>
    internal Lock Gate { get; } = new();

    private event Action<Run>? RunSettled;

    /// <summary>run 收口后的通知，回答等待方借此取回本轮产出。要求持有 Gate 触发。</summary>
    internal void NotifyRunSettled(Run run) => RunSettled?.Invoke(run);

    /// <summary>订阅 run 收口通知，返回解除订阅的委托。</summary>
    public Action SubscribeRunSettled(Action<Run> handler)
    {
        RunSettled += handler;

        return () => RunSettled -= handler;
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

    /// <summary>提交顺序排列的 run，宿主对话功能读数用。仅在持有 <see cref="Gate"/> 时读写。</summary>
    public IReadOnlyList<Run> Runs => _runs;

    /// <summary>任务的整体状态，由取消标记、执行节点状态与在跑的 run 汇总得出。</summary>
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
    /// <summary>要求持有 <see cref="Gate"/>。任务状态由取消标记、执行节点状态与在跑的 run 汇总得出，不单独维护。</summary>
    private TaskState Summarize()
    {
        if (_canceled)
        {
            return TaskState.Canceled;
        }

        if (_runs.Any(run => run.IsLive))
        {
            return TaskState.Running;
        }

        if (Runtime.HasBlocked)
        {
            return TaskState.Blocked;
        }

        return Runtime.HasWork ? TaskState.Running : TaskState.Done;
    }

    /// <summary>任务被取消：在跑的 run 与执行节点状态一并终止。要求持有 <see cref="Gate"/>。</summary>
    internal void Cancel()
    {
        _canceled = true;
        foreach (Run run in _runs.Where(run => run.IsLive))
        {
            run.Cancel();
        }

        Runtime.Cancel();
        _lastActivityAt = DateTimeOffset.UtcNow;
    }

    /// <summary>记下一个启动的 run 与它的执行回合，顺带在所属节点记账。要求持有 <see cref="Gate"/>。</summary>
    internal void Attach(Run run)
    {
        _runs.Add(run);
        Runtime.Executable(run.Context.NodeIndex).RecordRun(run.Context.ItemIndex);
        _lastActivityAt = DateTimeOffset.UtcNow;
        RunAttached?.Invoke(run);
    }

    private event Action<Run>? RunAttached;

    /// <summary>订阅新 run 启动的通知，回答等待方借此挂上流式观察。要求持有 Gate 触发。</summary>
    public Action SubscribeRunAttached(Action<Run> handler)
    {
        RunAttached += handler;

        return () => RunAttached -= handler;
    }

    internal void Touch() => _lastActivityAt = DateTimeOffset.UtcNow;

    /// <summary>当前状态的只读快照，含执行节点、执行节点状态、条目结论、run 与过程记录。</summary>
    public TaskSnapshot Snapshot()
    {
        lock (Gate)
        {
            Dictionary<RunId, RunSnapshot> runs = _runs.ToDictionary(run => run.Id, run => run.Snapshot());

            List<ExecutableSnapshot> executables =
            [
                .. Graph.ExecutableNodes.Select(node => new ExecutableSnapshot(node.Index, node,
                    [.. _runs.Where(run => run.Context.NodeIndex == node.Index).Select(run => runs[run.Id])])),
            ];

            List<ExecutableStateSnapshot> executableStates = [.. Graph.ExecutableNodes.Select(node => Runtime.SnapshotState(node.Index))];

            return new TaskSnapshot(Id, _title, Goal, Flow, Graph, Summarize(),
                _runs.Count(run => run.IsLive), Runtime.Splits(), executables, executableStates,
                Runtime.ContainerSnapshots(), Journal.Entries, Journal.DroppedEntries, _lastActivityAt);
        }
    }

    /// <summary>取目标的首行作标题，过长时截断。</summary>
    private static string Shorten(string text)
    {
        string first = text.Trim().Split('\n')[0].Trim();

        return first.Length <= TitleLimit ? first : string.Concat(first.AsSpan(0, TitleLimit), "…");
    }
}
