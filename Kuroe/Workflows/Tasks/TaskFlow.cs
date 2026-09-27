using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.Tasks;

/// <summary>一个执行节点的执行进度：实例展开集、在跑数与产出发表号。只在任务的 Gate 内读写。</summary>
sealed class NodeRunState
{
    /// <summary>PerItem 执行节点的实例条目集，Single 执行节点为空。实例集确定后不再变化。</summary>
    public IReadOnlyList<int> Items { get; set; } = [];

    /// <summary>实例集已确定。</summary>
    public bool Expanded { get; set; }

    /// <summary>在跑的实例数。</summary>
    public int Active;

    /// <summary>节点产出的发表号，拆分源也记在这里。</summary>
    public int Rev;

    /// <summary>逐实例的产出发表号。</summary>
    public Dictionary<int, int> ItemRev { get; } = [];

    /// <summary>产出停在等人批准。</summary>
    public bool Awaiting;

    /// <summary>停在等人返工或放行。</summary>
    public bool Blocked;

    /// <summary>随任务取消。</summary>
    public bool Canceled;
}

/// <summary>按图驱动的执行状态：激活、发布、返工与会合都集中在这里，是 DAG 执行模型的唯一可变成面。
/// 所有方法都要求调用者持有所属任务的 Gate。</summary>
internal sealed class TaskFlow
{
    private readonly WorkTask _task;
    private readonly Dictionary<int, NodeRunState> _states = [];
    private readonly Dictionary<(int Node, int? Item), CheckResult> _checks = [];
    private readonly Dictionary<(int Node, int? Item), int> _executions = [];
    private readonly Dictionary<int, IReadOnlyList<(int Node, int? Item)>> _reworkTargets = [];

    internal TaskFlow(WorkTask task)
    {
        _task = task;
    }

    /// <summary>任务锁定的流程编译视图。</summary>
    public NodeGraph Graph => _task.Graph;

    private NodeRunState State(int index)
    {
        if (!_states.TryGetValue(index, out NodeRunState? state))
        {
            state = new NodeRunState();
            _states[index] = state;
        }

        return state;
    }

    // ---- 实例展开 ----

    /// <summary>PerItem 执行节点的实例条目集。拆分来源给分支过滤后的条目，否则取对齐来源的并集。尚未可用时为空。</summary>
    private List<int> ResolveItems(int node)
    {
        NodeRunState state = State(node);
        if (state.Expanded)
        {
            return [.. state.Items];
        }

        ExecutableNode executable = Graph[node];
        List<int> items = [];
        if (Graph.ItemSource(node) is { } plan && _task.SplitFor(plan) is { } output)
        {
            items = [.. output.Items
                .Where(item => executable.Branch is null || item.Branch == executable.Branch)
                .Select(item => item.Index)];
        }
        else
        {
            foreach (FlowEdge edge in Graph.Incoming(node))
            {
                if (edge.Feed == EdgeFeed.Aligned && State(edge.From).Expanded)
                {
                    items.AddRange(State(edge.From).Items);
                }
            }

            items = [.. items.Distinct()];
        }

        if (items.Count > 0)
        {
            state.Items = items;
            state.Expanded = true;
        }

        return items;
    }

    // ---- 就绪判定 ----

    /// <summary>一条入边当前是否可消费：来源已经有目标要等的那份产出。</summary>
    private bool FeedSatisfied(FlowEdge edge, int? item)
    {
        NodeRunState? source = _states.GetValueOrDefault(edge.From);
        switch (edge.Feed)
        {
            case EdgeFeed.Single:
                return source is { Rev: > 0, Canceled: false };

            case EdgeFeed.Items:
                return _task.SplitFor(edge.From) is not null;

            case EdgeFeed.AllInstances:
                return source is { Expanded: true } && source.ItemRev.Count >= source.Items.Count;

            case EdgeFeed.Aligned:
                if (item is not { } index)
                {
                    return true;
                }

                if (source is null || !source.Expanded)
                {
                    return false;
                }

                // 已展开且承担该条目才需要它的产出
                return !source.Items.Contains(index) || source.ItemRev.ContainsKey(index);

            default:
                return false;
        }
    }

    private bool ItemSatisfied(int node, int? item) =>
        Graph.Incoming(node).All(edge => FeedSatisfied(edge, item));

    /// <summary>实例是否已有产出。</summary>
    public bool Complete(int node, int? item)
    {
        NodeRunState? state = _states.GetValueOrDefault(node);
        return state is not null && (item is { } i ? state.ItemRev.ContainsKey(i) : state.Rev > 0);
    }

    /// <summary>执行节点是否还有待启动的实例。Single 执行节点一次执行，PerItem 执行节点按实例逐步启动。</summary>
    public bool CanStart(int node)
    {
        NodeRunState? state = _states.GetValueOrDefault(node);
        if (state is { Active: > 0 } or { Awaiting: true } or { Blocked: true } or { Canceled: true })
        {
            return false;
        }

        ExecutableNode executable = Graph[node];
        if (executable.Mode != NodeMode.PerItem)
        {
            return !Complete(node, null) && ItemSatisfied(node, null);
        }

        return ResolveItems(node).Any(item => !Complete(node, item) && ItemSatisfied(node, item));
    }

    /// <summary>把执行节点可启动的实例取出来启动。调用者应先用 <see cref="CanStart"/> 判定。</summary>
    public IReadOnlyList<RunStarter> Start(int node)
    {
        NodeRunState state = State(node);
        List<RunStarter> starters = [];
        ExecutableNode executable = Graph[node];
        if (executable.Mode == NodeMode.PerItem)
        {
            foreach (int item in ResolveItems(node).Where(item => !Complete(node, item) && ItemSatisfied(node, item)))
            {
                _executions[(node, item)] = _executions.GetValueOrDefault((node, item)) + 1;
                starters.Add(new RunStarter(item));
            }
        }
        else if (!Complete(node, null) && ItemSatisfied(node, null))
        {
            _executions[(node, null)] = _executions.GetValueOrDefault((node, null)) + 1;
            starters.Add(new RunStarter(null));
        }

        if (starters.Count > 0)
        {
            state.Active += starters.Count;
            state.Awaiting = false;
        }

        return starters;
    }

    /// <summary>某实例在当前节点的执行次数，首次启动为 1。要求持有 Gate。</summary>
    public int ExecutionCount(int node, int? item) => _executions.GetValueOrDefault((node, item));

    /// <summary>一个待派发的实例：条目序可为空（整节点）。</summary>
    public readonly record struct RunStarter(int? Item);

    /// <summary>把节点从等待批准中放行，返回是否确实在等待。</summary>
    public bool Release(int node)
    {
        NodeRunState state = State(node);
        if (!state.Awaiting)
        {
            return false;
        }

        state.Awaiting = false;

        return true;
    }

    // ---- 发布与作废 ----

    /// <summary>实例或整节点产出的发表：发表号递增，下游据此重新评估。</summary>
    public void Publish(int node, int? item)
    {
        NodeRunState state = State(node);
        if (item is { } index)
        {
            state.ItemRev[index] = state.ItemRev.GetValueOrDefault(index) + 1;
        }
        else
        {
            state.Rev++;
        }
    }

    /// <summary>作废实例或整节点的已发表产出，返工起点用它让下游重新等待。PerItem 执行节点不带条目时清空全部实例。</summary>
    public void Invalidate(int node, int? item)
    {
        NodeRunState state = State(node);
        if (item is { } index)
        {
            state.ItemRev.Remove(index);
        }
        else if (Graph[node].Mode == NodeMode.PerItem)
        {
            state.ItemRev.Clear();
        }
        else
        {
            state.Rev = 0;
        }
    }

    // ---- 检查结论 ----

    /// <summary>最近一次检查结论，未检查时为空。</summary>
    public CheckResult? LastCheck(int node, int? item) => _checks.GetValueOrDefault((node, item));

    /// <summary>记录一轮检查结论，同一次检查只认首个交点。返回是否记录成功。</summary>
    public bool RecordCheck(int node, int? item, CheckResult check)
    {
        if (_checks.TryGetValue((node, item), out CheckResult? last) && last.Origin == check.Origin)
        {
            return false;
        }

        _checks[(node, item)] = check;

        return true;
    }

    // ---- 汇总 ----

    /// <summary>一个已派发的实例终结，在跑数减一。要求持有 Gate。</summary>
    public void ReleaseActive(int node)
    {
        NodeRunState state = State(node);
        if (state.Active > 0)
        {
            state.Active--;
        }
    }

    /// <summary>把执行节点置为阻塞并记下返工目标，恢复时按目标作废并重跑，重复阻塞合并去重。要求持有 Gate。</summary>
    public void Block(int node, IReadOnlyList<(int Node, int? Item)> targets)
    {
        State(node).Blocked = true;
        IEnumerable<(int Node, int? Item)> merged = _reworkTargets.TryGetValue(node, out var previous)
            ? previous!.Concat(targets).Distinct()
            : targets;
        _reworkTargets[node] = [.. merged];
    }

    /// <summary>是否有执行节点停在等人批准。</summary>
    public bool HasAwaiting => _states.Values.Any(state => state.Awaiting);

    /// <summary>是否有执行节点停在等人返工。</summary>
    public bool HasBlocked => _states.Values.Any(state => state.Blocked);

    /// <summary>还有在跑的 run 或可启动的执行节点，任务就没走完。</summary>
    public bool HasWork
    {
        get
        {
            if (_states.Values.Any(state => state.Active > 0))
            {
                return true;
            }

            for (int index = 0; index < Graph.Count; index++)
            {
                if (CanStart(index))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>取消任务：全部执行节点进入取消态。</summary>
    public void Cancel()
    {
        foreach (NodeRunState state in _states.Values)
        {
            state.Canceled = true;
        }
    }

    // ---- 返工 ----

    /// <summary>检查节点返工要退回的实施来源。</summary>
    public IReadOnlyList<int> ReworkTargets(int checkNode) => Graph.CheckedSources(checkNode);

    /// <summary>逐条返工要退回的实施来源：只退回实际承担该条目的执行节点。</summary>
    public IReadOnlyList<int> ReworkTargets(int checkNode, int item)
    {
        List<int> targets = [];
        foreach (int source in Graph.CheckedSources(checkNode))
        {
            NodeRunState? state = _states.GetValueOrDefault(source);
            if (state is { Expanded: true } && state.Items.Contains(item))
            {
                targets.Add(source);
            }
        }

        return targets;
    }

    /// <summary>顺着出边要通知的下游执行节点，去重保持顺序。</summary>
    public IReadOnlyList<int> Downstream(int node) =>
        [.. Graph.Outgoing(node).Select(edge => edge.To).Distinct()];

    /// <summary>门控检查：产出后停在待批准，返回是否停留。</summary>
    private bool Park(int node)
    {
        if (Graph[node].Gate != NodeGate.Review)
        {
            return false;
        }

        State(node).Awaiting = true;

        return true;
    }

    /// <summary>run 收口的决策结果：要通知的执行节点，或自动返工重派的实例。</summary>
    public sealed record SettlePlan(IReadOnlyList<int> Notify, IReadOnlyList<(int Node, int? Item)> Rerun);

    /// <summary>run 收口：记产出、处理门控与检查结论，产出出向下游的通知或重派计划。</summary>
    public SettlePlan Settle(Run run)
    {
        int node = run.Context.NodeIndex;
        int? item = run.Context.ItemIndex;
        ExecutableNode executable = Graph[node];

        if (executable.Output == NodeOutput.Plan)
        {
            // 拆分已由回执写入 _splits
            Publish(node, null);

            return Park(node) ? new SettlePlan([], []) : new SettlePlan(Downstream(node), []);
        }

        if (executable.Output == NodeOutput.Review)
        {
            CheckResult? check = LastCheck(node, item);
            if (check is { Passed: true })
            {
                Publish(node, item);

                return Park(node) ? new SettlePlan([], []) : new SettlePlan(Downstream(node), []);
            }

            if (Retryable(executable, check))
            {
                List<(int Node, int? Item)> rerun = [];
                foreach (int source in item is { } index ? ReworkTargets(node, index) : ReworkTargets(node))
                {
                    Invalidate(source, item);
                    rerun.Add((source, item));
                }

                return new SettlePlan([], rerun);
            }

            IReadOnlyList<int> sources = item is { } failed ? ReworkTargets(node, failed) : ReworkTargets(node);
            Block(node, [.. sources.Select(source => (source, item))]);

            return new SettlePlan([], []);
        }

        Publish(node, item);

        return Park(node) ? new SettlePlan([], []) : new SettlePlan(Downstream(node), []);
    }

    /// <summary>检查不通过且未达到轮次上限时自动退回返工。</summary>
    private static bool Retryable(ExecutableNode executable, CheckResult? check)
    {
        if (executable.RejectAction == RejectAction.Stop)
        {
            return false;
        }

        int round = check?.Round ?? 1;

        return round < executable.AttemptLimit;
    }

    /// <summary>执行节点在快照里的一刻状态。</summary>
    public NodeStateSnapshot NodeStateSnapshot(int index)
    {
        NodeRunState? s = _states.GetValueOrDefault(index);
        if (s is null)
        {
            return new NodeStateSnapshot(index, NodeState.Pending, [], 0);
        }

        List<int> items = s.Expanded ? [.. s.Items] : [];
        int completed;
        if (s.Expanded)
        {
            completed = items.Count(item => s.ItemRev.ContainsKey(item));
        }
        else
        {
            completed = s.Rev > 0 ? 1 : 0;
        }

        NodeState state;
        if (s.Canceled)
        {
            state = NodeState.Canceled;
        }
        else if (s.Awaiting)
        {
            state = NodeState.AwaitingApproval;
        }
        else if (s.Blocked)
        {
            state = NodeState.Blocked;
        }
        else if (s.Active > 0)
        {
            state = NodeState.Running;
        }
        else if (s.Expanded)
        {
            state = completed >= items.Count ? NodeState.Done : NodeState.Running;
        }
        else
        {
            state = s.Rev > 0 ? NodeState.Done : NodeState.Pending;
        }

        return new NodeStateSnapshot(index, state, items, completed);
    }

    /// <summary>执行节点已确定的实例集，尚未展开时为空。</summary>
    public IReadOnlyList<int>? ExpandedItems(int node)
    {
        NodeRunState? s = _states.GetValueOrDefault(node);
        return s is { Expanded: true } ? [.. s.Items] : null;
    }

    // ---- 宿主入口 ----

    /// <summary>一个执行节点被激活后的评估：静态拆分就地产出、等待批准的放行、输入齐备启动实例。
    /// 返回要通知的下游与要派发的实例。要求持有 Gate。</summary>
    public EvaluateResult Evaluate(int node)
    {
        ExecutableNode executable = Graph[node];
        if (executable.IsStaticSplit && _task.SplitFor(node) is null)
        {
            // 纯静态拆分不派 run，激活即产出
            _task.SetSplit(node, new PlanOutput(new RunId(0), SplitMerge.Apply(executable.Split!, []).Value));
            Publish(node, null);

            return new EvaluateResult(Downstream(node), []);
        }

        if (Release(node))
        {
            return new EvaluateResult(Downstream(node), []);
        }

        if (CanStart(node))
        {
            return new EvaluateResult([], Start(node));
        }

        return new EvaluateResult([], []);
    }

    /// <summary>一次评估的结果：要通知的下游执行节点与要派发的实例。</summary>
    public readonly record struct EvaluateResult(IReadOnlyList<int> Notify, IReadOnlyList<RunStarter> Starters);

    /// <summary>无入边的执行节点，任务启动的第一批。</summary>
    public IReadOnlyList<int> Roots() =>
        [.. Enumerable.Range(0, Graph.Count).Where(index => Graph.Incoming(index).Count == 0)];

    /// <summary>正在等人批准的执行节点。</summary>
    public IReadOnlyList<int> AwaitingNodes => [.. _states.Where(pair => pair.Value.Awaiting).Select(pair => pair.Key)];

    /// <summary>被阻塞的执行节点，等人返工或放行。</summary>
    public IReadOnlyList<int> BlockedNodes => [.. _states.Where(pair => pair.Value.Blocked).Select(pair => pair.Key)];

    /// <summary>计算返工会作废并重跑的目标：各阻塞节点记录的目标按条目过滤，配对其阻塞节点。不改变状态。要求持有 Gate。</summary>
    public IReadOnlyList<(int Blocked, int Node, int? Item)> PlanRework(int? itemIndex)
    {
        List<(int Blocked, int Node, int? Item)> targets = [];
        foreach (int blocked in BlockedNodes)
        {
            foreach ((int node, int? item) in _reworkTargets.GetValueOrDefault(blocked) ?? [])
            {
                if (itemIndex is { } requested && item != requested)
                {
                    continue;
                }

                targets.Add((blocked, node, item));
            }
        }

        return targets;
    }

    /// <summary>解除执行节点的阻塞：移除本次返工处理的目标，剩余目标保持阻塞，全部处理后解除阻塞。要求持有 Gate。</summary>
    public void Unblock(int node, IReadOnlyList<(int Node, int? Item)> handled)
    {
        List<(int Node, int? Item)> remaining = [];
        foreach ((int n, int? item) in _reworkTargets.GetValueOrDefault(node) ?? [])
        {
            if (!handled.Contains((n, item)))
            {
                remaining.Add((n, item));
            }
        }

        if (remaining.Count == 0)
        {
            State(node).Blocked = false;
            _reworkTargets.Remove(node);
        }
        else
        {
            _reworkTargets[node] = remaining;
        }
    }

    /// <summary>实施执行节点要读的返工意见：引用它的检查执行节点最近一次拒绝结论。检查执行节点读自己的结论。要求持有 Gate。</summary>
    public CheckResult? ReworkFor(int executableIndex, int? itemIndex)
    {
        if (Graph[executableIndex].Output == NodeOutput.Review)
        {
            CheckResult? check = LastCheck(executableIndex, itemIndex);

            return check is { Passed: false } ? check : null;
        }

        foreach (int check in Graph.ExecutableNodes
                     .Where(candidate => candidate.Output == NodeOutput.Review)
                     .Select(candidate => candidate.Index))
        {
            if (!Graph.CheckedSources(check).Contains(executableIndex))
            {
                continue;
            }

            CheckResult? error = itemIndex is { } index
                ? LastCheck(check, index) ?? LastCheck(check, null)
                : LastCheck(check, null);
            if (error is { Passed: false })
            {
                return error;
            }
        }

        return null;
    }
}