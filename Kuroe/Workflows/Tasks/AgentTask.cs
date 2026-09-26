using ErrorOr;
using Kuroe.Agent.Runs;
using Kuroe.Agent.Sessions;
using Kuroe.Agent.Turns;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Agent.Turns;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.Tasks;

/// <summary>任务 = 一条长期会话线程：自己的对话历史、按流程推进的执行单元、派出的全部 agent。
/// 可变成只经内部方法改动，宿主读 <see cref="Snapshot"/>。</summary>
public sealed class AgentTask
{
    /// <summary>前台对话回合在过程记录里的叶子名。</summary>
    public const string DialogueNode = "对话";

    private const int TitleLimit = 40;

    private readonly List<AgentRun> _runs = [];
    private readonly List<WorkUnit> _units = [];
    private readonly Dictionary<int, PlanOutput> _splits = [];
    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly Dictionary<string, CheckResult> _funnels = [];
    private readonly HashSet<int> _funnelActive = [];
    private string _title;
    private int _dialogueTurns;
    private DateTimeOffset _lastActivityAt;
    private bool _canceled;

    internal AgentTask(TaskId id, string goal, Workflow flow, NodeGraph graph, AgentSession session, string? title)
    {
        Id = id;
        Goal = goal;
        Flow = flow;
        Graph = graph;
        Session = session;
        Journal = new TurnJournal();
        _lastActivityAt = DateTimeOffset.UtcNow;
        _title = string.IsNullOrWhiteSpace(title) ? Shorten(goal) : title;
    }

    /// <summary>任务标识。</summary>
    public TaskId Id { get; }

    /// <summary>提交时给出的目标。</summary>
    public string Goal { get; }

    /// <summary>提交时锁定的流程模板。</summary>
    public Workflow Flow { get; }

    /// <summary>提交时锁定的流程编译视图。</summary>
    internal NodeGraph Graph { get; }

    internal AgentSession Session { get; }

    /// <summary>前台对话的过程记录。</summary>
    internal TurnJournal Journal { get; }

    /// <summary>任务内状态改动的串行点。锁序固定为任务 Gate、注册表、派发器。</summary>
    internal Lock Gate { get; } = new();

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

    /// <summary>各叶子交回的条目拆分，键是产出叶子序号，尚未交回时为空。仅在持有 <see cref="Gate"/> 时读写。</summary>
    public IReadOnlyDictionary<int, PlanOutput> Splits => _splits;

    /// <summary>某叶子的条目拆分，尚未交回时为空。要求持有 <see cref="Gate"/>。</summary>
    internal PlanOutput? SplitFor(int leafIndex) => _splits.GetValueOrDefault(leafIndex);

    /// <summary>记录某叶子的条目拆分。要求持有 <see cref="Gate"/>。</summary>
    internal void SetSplit(int leafIndex, PlanOutput output) => _splits[leafIndex] = output;

    /// <summary>提交顺序排列的 agent。仅在持有 <see cref="Gate"/> 时读写。</summary>
    internal IReadOnlyList<AgentRun> Runs => _runs;

    /// <summary>创建顺序排列的工作单元：整步单元在前，按条目展开出的单元随后。仅在持有 <see cref="Gate"/> 时读写。</summary>
    internal IReadOnlyList<WorkUnit> Units => _units;

    /// <summary>任务的整体状态，由取消标记、各单元与在跑的 agent 汇总得出。</summary>
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

    /// <summary>要求持有 <see cref="Gate"/>。任务状态由取消标记、单元状态与在跑的 agent 汇总得出，不单独维护。</summary>
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

        if (_units.Any(unit => unit.State == UnitState.AwaitingApproval))
        {
            return TaskState.AwaitingApproval;
        }

        if (_units.Any(unit => unit.State == UnitState.Blocked))
        {
            return TaskState.Blocked;
        }

        // 还有单元没走完最后一步，或一步都没派出去
        return _units.All(unit => unit.State == UnitState.Done) ? TaskState.Done : TaskState.Running;
    }

    /// <summary>任务被取消：在跑的 agent 与未走完的单元一并终止。要求持有 <see cref="Gate"/>。</summary>
    internal void Cancel()
    {
        _canceled = true;
        foreach (AgentRun run in _runs.Where(run => run.IsLive))
        {
            run.Cancel();
        }

        foreach (WorkUnit unit in _units.Where(unit => unit.State != UnitState.Done))
        {
            unit.Cancel();
        }

        _lastActivityAt = DateTimeOffset.UtcNow;
    }
    /// <summary>收拢检查的最近一次结论，未检查时为空。要求持有 <see cref="Gate"/>。</summary>
    internal CheckResult? FunnelCheck(string nodeName) => _funnels.GetValueOrDefault(nodeName);

    /// <summary>抢占收拢检查的活动，成功时给出本轮轮次。要求持有 <see cref="Gate"/>。</summary>
    internal bool TryBeginFunnel(int nodeIndex, string nodeName, out int round)
    {
        if (!_funnelActive.Add(nodeIndex))
        {
            round = 0;

            return false;
        }

        round = (FunnelCheck(nodeName)?.Round ?? 0) + 1;

        return true;
    }

    /// <summary>收拢检查收口后释放活动。要求持有 <see cref="Gate"/>。</summary>
    internal void EndFunnel(int nodeIndex) => _funnelActive.Remove(nodeIndex);

    /// <summary>收拢检查结论落地。同一次检查只有首个交点，交回文本给模型。要求持有 <see cref="Gate"/>。</summary>
    internal bool RecordFunnelCheck(AgentRun run, string nodeName, bool passed, string findings)
    {
        if (_funnels.TryGetValue(nodeName, out CheckResult? last) && last.Origin == run.Id)
        {
            return false;
        }

        _funnels[nodeName] = new CheckResult(run.Context.Attempt, passed, findings, run.Id, nodeName);

        return true;
    }

    internal WorkUnit AddUnit(int? itemIndex, PlanItem? item, int cursor, string? branch = null)
    {
        WorkUnit unit = new(itemIndex, item, cursor, branch);
        _units.Add(unit);

        return unit;
    }

    /// <summary>按条目定位单元；不展开的叶子只有一个单元。要求持有 <see cref="Gate"/>。</summary>
    internal WorkUnit? UnitFor(int? itemIndex) => _units.Find(unit => unit.ItemIndex == itemIndex);

    /// <summary>把 agent 挂到任务的单元与该叶子上。</summary>
    internal void Attach(WorkUnit unit, AgentRun run)
    {
        _runs.Add(run);
        unit.Attach(run);
        _lastActivityAt = DateTimeOffset.UtcNow;
    }

    /// <summary>收拢检查的 agent 没有单元载体，只挂到任务的运行统计。</summary>
    internal void AttachRun(AgentRun run)
    {
        _runs.Add(run);
        _lastActivityAt = DateTimeOffset.UtcNow;
    }

    internal void Touch() => _lastActivityAt = DateTimeOffset.UtcNow;

    /// <summary>当前任务的一轮前台对话。增量交给 observer，过程同时记进任务的记录。</summary>
    public async Task<ErrorOr<DialogueReply>> AskDialogueAsync(string input, ITurnSink? observer, CancellationToken cancellationToken)
    {
        if (!await _turn.WaitAsync(0, cancellationToken))
        {
            return [TaskErrors.Busy(Id)];
        }

        try
        {
            TurnScope scope;
            lock (Gate)
            {
                _dialogueTurns++;
                _lastActivityAt = DateTimeOffset.UtcNow;
                scope = new TurnScope
                {
                    Task = Id,
                    Run = null,
                    Output = null,
                    NodeName = DialogueNode,
                    ItemIndex = null,
                    Journal = Journal,
                    Sink = TurnSinks.For(Journal, observer),
                };
            }

            ErrorOr<string> reply = await Session.AskAsync(input, scope, cancellationToken);

            return reply.IsError
                ? reply.ErrorsOrEmptyList
                : new DialogueReply(reply.Value, Session.LastTurnDiscarded);
        }
        finally
        {
            _turn.Release();
        }
    }

    /// <summary>丢弃前台对话的上下文，回到起点。</summary>
    public void ResetDialogue() => Session.Reset();

    /// <summary>把 agent 的结论作为一条用户消息写进会话历史，后续对话才用得上它。</summary>
    internal void Adopt(string text)
    {
        lock (Gate)
        {
            Session.Adopt(text);
            Journal.Append(new PromptEntry(text));
            _lastActivityAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>当前状态的只读快照，含节点、单元、agent 与前台对话。</summary>
    public TaskSnapshot Snapshot()
    {
        lock (Gate)
        {
            Dictionary<RunId, RunSnapshot> runs = _runs.ToDictionary(run => run.Id, run => run.Snapshot());

            List<NodeSnapshot> nodes =
            [
                .. Graph.Leaves.Select((leaf, index) => new NodeSnapshot(index, leaf,
                    [.. _runs.Where(run => run.Context.NodeIndex == index).Select(run => runs[run.Id])])),
            ];

            List<UnitSnapshot> units =
            [
                .. _units.Select(unit => new UnitSnapshot(unit.ItemIndex, unit.Branch, unit.NodeCursor, unit.State,
                    unit.Verdict, unit.Findings, unit.Attempts, [.. unit.Nodes.Values.Select(run => runs[run.Id])])),
            ];

            return new TaskSnapshot(Id, _title, Goal, Flow, Graph, Summarize(), _dialogueTurns,
                _runs.Count(run => run.IsLive), new Dictionary<int, PlanOutput>(_splits), nodes, units,
                Journal.Entries, Journal.DroppedEntries, _lastActivityAt);
        }
    }

    /// <summary>取目标的首行作标题，过长时截断。</summary>
    private static string Shorten(string text)
    {
        string first = text.Trim().Split('\n')[0].Trim();

        return first.Length <= TitleLimit ? first : string.Concat(first.AsSpan(0, TitleLimit), "…");
    }
}
