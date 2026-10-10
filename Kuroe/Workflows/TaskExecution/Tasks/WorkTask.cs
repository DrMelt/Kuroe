using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Executions.Turns;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.TaskExecution.Tasks;

/// <summary>任务 = 一条长期执行线程：按流程推进的执行节点状态与启动的全部 run。
/// 可变成只经内部方法改动，宿主读 <see cref="Snapshot"/>。</summary>
public sealed class WorkTask
{
    private const int TitleLimit = 40;

    private readonly List<Run> _runs = [];
    private readonly Dictionary<int, PlanOutput> _splits = [];
    private readonly Dictionary<(int Node, int? Item), IReadOnlyDictionary<PortName, string>> _portValues = [];
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

    /// <summary>任务的过程记录，只记执行侧诊断内容。</summary>
    internal TurnJournal Journal { get; }

    /// <summary>任务内状态改动的串行点。锁序固定为任务 Gate、注册表、派发器。</summary>
    internal Lock Gate { get; } = new();

    private event Action<Run>? RunSettled;

    /// <summary>run 收口后的通知，回答等待方借此取回本轮产出。要求持有 Gate 触发。</summary>
    internal void NotifyRunSettled(Run run) => RunSettled?.Invoke(run);

    /// <summary>订阅 run 收口通知，返回解除订阅的委托。</summary>
    internal Action SubscribeRunSettled(Action<Run> handler)
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

    /// <summary>各执行节点交回的条目拆分，键是产出执行节点序号，尚未交回时为空。仅在持有 <see cref="Gate"/> 时读写。</summary>
    public IReadOnlyDictionary<int, PlanOutput> Splits => _splits;

    /// <summary>某执行节点的条目拆分，尚未交回时为空。要求持有 <see cref="Gate"/>。</summary>
    internal PlanOutput? SplitFor(int executableIndex) => _splits.GetValueOrDefault(executableIndex);

    /// <summary>记录某执行节点的条目拆分。要求持有 <see cref="Gate"/>。</summary>
    internal void SetSplit(int executableIndex, PlanOutput output) => _splits[executableIndex] = output;

    /// <summary>某执行节点某实例交回的命名输出端口值，整节点实例不写实例序，尚未交回时为空。要求持有 <see cref="Gate"/>。</summary>
    internal IReadOnlyDictionary<PortName, string>? PortValuesFor(int executableIndex, int? item) =>
        _portValues.GetValueOrDefault((executableIndex, item));

    /// <summary>记录某执行节点某实例的输出端口值。要求持有 <see cref="Gate"/>。</summary>
    internal void SetPortValues(int executableIndex, int? item, IReadOnlyDictionary<PortName, string> values) =>
        _portValues[(executableIndex, item)] = values;

    /// <summary>移除某执行节点某实例的端口值，item 为空时清空该节点全部实例，返工作废后端口随产出重交。要求持有 <see cref="Gate"/>。</summary>
    internal void ClearPortValues(int executableIndex, int? item)
    {
        if (item is { } index)
        {
            _portValues.Remove((executableIndex, index));
            return;
        }

        foreach ((int node, int? entry) in _portValues.Keys.Where(pair => pair.Node == executableIndex).ToList())
        {
            _portValues.Remove((node, entry));
        }
    }

    /// <summary>提交顺序排列的 run。仅在持有 <see cref="Gate"/> 时读写。</summary>
    internal IReadOnlyList<Run> Runs => _runs;

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

        if (Runtime.HasAwaitingInput)
        {
            return TaskState.AwaitingInput;
        }

        if (Runtime.HasAwaiting)
        {
            return TaskState.AwaitingApproval;
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
    internal Action SubscribeRunAttached(Action<Run> handler)
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
                _runs.Count(run => run.IsLive), new Dictionary<int, PlanOutput>(_splits), executables, executableStates,
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
