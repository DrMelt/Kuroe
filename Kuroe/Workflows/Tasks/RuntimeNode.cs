using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.Tasks;

/// <summary>一个节点在任务内的运行时对象：节点定义与节点自身状态的合体。
/// 执行节点由推进方法改动状态，容器随成员推进经 TaskFlow 刷新。读写都要求持有任务 Gate。</summary>
internal abstract class RuntimeNode(GraphNode node)
{
    /// <summary>提交时锁定的节点定义，不可变。</summary>
    public GraphNode Node { get; } = node;

    /// <summary>节点在图里的序号，框架路由与快照定位以它为准。</summary>
    public int Index => Node.Index;

    /// <summary>节点名。</summary>
    public NodeName Name => Node.Name;

    /// <summary>节点进展到待批点时是否停人批准。</summary>
    public NodeGate Gate => Node.Gate;

    /// <summary>产出已放行、可被下游消费：执行节点按自身发布与停驻，容器递归成员与子容器。</summary>
    public abstract bool Released { get; }

    /// <summary>产出停在等人批准。</summary>
    public abstract bool Awaiting { get; }

    /// <summary>随任务取消。</summary>
    public abstract bool Canceled { get; }

    /// <summary>已放行产出的位置集合：执行节点给整节点或已发布实例，容器递归给全部成员。</summary>
    public abstract IReadOnlyList<(int Node, int? Item)> ReleasedOutputs();

    /// <summary>随任务进入取消态。</summary>
    public abstract void Cancel();
}

/// <summary>一个执行节点在任务内的运行时对象：执行节点定义与节点自身状态的合体。
/// 节点状态只经推进方法改动，读写都要求持有任务 Gate。</summary>
internal sealed class RuntimeExecutable(ExecutableNode executable) : RuntimeNode(executable)
{
    /// <summary>提交时锁定的执行节点定义，不可变。</summary>
    public ExecutableNode Executable { get; } = executable;

    /// <summary>执行节点的产出契约，决定收口方式与契约工具。</summary>
    public NodeOutput Output => Executable.Output;

    /// <summary>执行节点的展开模式：整节点一次执行还是按实例逐步启动。</summary>
    public NodeMode Mode => Executable.Mode;

    /// <summary>PerItem 执行节点的实例条目集，Single 执行节点为空。实例集确定后不再变化。</summary>
    public IReadOnlyList<int> Items { get; private set; } = [];

    /// <summary>实例集已确定。</summary>
    public bool Expanded { get; private set; }

    /// <summary>在跑的实例数。推进时经 AddActive 与 ReleaseActive 改动。</summary>
    public int Active { get; private set; }

    /// <summary>节点产出的发表号，拆分源也记在这里。</summary>
    public int Rev { get; private set; }

    /// <summary>逐实例的产出发表号。返工作废后对应项移除。只经 Publish 与 Invalidate 改动。</summary>
    private readonly Dictionary<int, int> _itemRev = [];

    private bool _awaiting;
    private bool _blocked;
    private bool _canceled;

    public override bool Awaiting => _awaiting;

    /// <summary>停在等人返工或放行。</summary>
    public bool Blocked => _blocked;

    public override bool Canceled => _canceled;

    /// <summary>产出已放行、可被下游消费：产出已发布且不在等待、阻塞与取消。PerItem 要求全部实例已发布。</summary>
    public override bool Released =>
        (Mode == NodeMode.PerItem
            ? Expanded && Items.All(item => _itemRev.ContainsKey(item))
            : Rev > 0)
        && !Awaiting && !Blocked && !Canceled;

    /// <summary>整节点的最近检查结论，Single 检查执行节点使用。</summary>
    public CheckResult? WholeCheck { get; private set; }

    /// <summary>逐实例的最近检查结论，PerItem 检查执行节点使用。只经 RecordCheck 改动。</summary>
    private readonly Dictionary<int, CheckResult> _itemChecks = [];

    /// <summary>整节点的执行次数，首次启动为 1。</summary>
    public int WholeExecutions { get; private set; }

    /// <summary>逐实例的执行次数，首次启动为 1。只经 RecordExecution 改动。</summary>
    private readonly Dictionary<int, int> _itemExecutions = [];

    private readonly List<(int Node, int? Item)> _reworkTargets = [];

    /// <summary>阻塞待返工时作的执行节点与条目，全部处理后解除阻塞。</summary>
    public IReadOnlyList<(int Node, int? Item)> ReworkTargets => _reworkTargets;

    /// <summary>是否仍有在跑的实例。</summary>
    public bool HasActive => Active > 0;

    /// <summary>已确定的实例集，尚未展开时为空。</summary>
    public IReadOnlyList<int>? ExpandedItems => Expanded ? [.. Items] : null;

    /// <summary>固定实例集并标为已展开，PerItem 实例展开的落点。</summary>
    public void AdoptItems(IReadOnlyList<int> items)
    {
        Items = items;
        Expanded = true;
    }

    /// <summary>实例集已展开且承担该条目。</summary>
    public bool HasItem(int index) => Expanded && Items.Contains(index);

    /// <summary>实例或整节点是否已有发表产出。</summary>
    public bool Complete(int? item) => item is { } index ? _itemRev.ContainsKey(index) : Rev > 0;

    /// <summary>实例或整节点产出的发表：发表号递增，下游据此重新评估。</summary>
    public void Publish(int? item)
    {
        if (item is { } index)
        {
            _itemRev[index] = _itemRev.GetValueOrDefault(index) + 1;
        }
        else
        {
            Rev++;
        }
    }

    /// <summary>作废实例或整节点的已发表产出，返工起点用它让下游重新等待。PerItem 执行节点不带条目时清空全部实例。</summary>
    public void Invalidate(int? item)
    {
        if (item is { } index)
        {
            _itemRev.Remove(index);
        }
        else if (Mode == NodeMode.PerItem)
        {
            _itemRev.Clear();
        }
        else
        {
            Rev = 0;
        }
    }

    /// <summary>记一轮执行，次数按实例或整节点累计。</summary>
    public void RecordExecution(int? item)
    {
        if (item is { } index)
        {
            _itemExecutions[index] = _itemExecutions.GetValueOrDefault(index) + 1;
        }
        else
        {
            WholeExecutions++;
        }
    }

    /// <summary>某实例或整节点在当前节点的执行次数，首次启动为 1。</summary>
    public int ExecutionCount(int? item) =>
        item is { } index ? _itemExecutions.GetValueOrDefault(index) : WholeExecutions;

    /// <summary>一个已派发的实例终结，在跑数减一。</summary>
    public void ReleaseActive()
    {
        if (Active > 0)
        {
            Active--;
        }
    }

    /// <summary>记下新派发的实例数。</summary>
    public void AddActive(int count) => Active += count;

    /// <summary>清除等待批准，返回是否确实在等。</summary>
    public bool ClearAwaiting()
    {
        if (!_awaiting)
        {
            return false;
        }

        _awaiting = false;

        return true;
    }

    /// <summary>产出后按门控停在等人批准，Auto 不停。返回是否停留。</summary>
    public bool Park()
    {
        if (Gate != NodeGate.Review)
        {
            return false;
        }

        _awaiting = true;

        return true;
    }

    /// <summary>置为阻塞并合并记下返工目标，重复目标不重复收集。</summary>
    public void EnterBlocked(IReadOnlyList<(int Node, int? Item)> targets)
    {
        _blocked = true;
        foreach ((int node, int? item) in targets)
        {
            if (!_reworkTargets.Contains((node, item)))
            {
                _reworkTargets.Add((node, item));
            }
        }
    }

    /// <summary>移除本次返工处理的目标，全部处理后解除阻塞。</summary>
    public void FinishRework(IReadOnlyList<(int Node, int? Item)> handled)
    {
        foreach ((int node, int? item) in handled)
        {
            _reworkTargets.Remove((node, item));
        }

        if (_reworkTargets.Count == 0)
        {
            _blocked = false;
        }
    }

    /// <summary>随任务进入取消态。</summary>
    public override void Cancel() => _canceled = true;

    /// <summary>记录一轮检查结论，同一交回者只认首个交点。返回是否记录成功。</summary>
    public bool RecordCheck(int? item, CheckResult check)
    {
        if (item is { } index)
        {
            if (_itemChecks.TryGetValue(index, out CheckResult? last) && last.Origin == check.Origin)
            {
                return false;
            }

            _itemChecks[index] = check;

            return true;
        }

        if (WholeCheck is { Origin: var origin } && origin == check.Origin)
        {
            return false;
        }

        WholeCheck = check;

        return true;
    }

    /// <summary>最近一次检查结论，未检查时为空。</summary>
    public CheckResult? LastCheck(int? item) =>
        item is { } index ? _itemChecks.GetValueOrDefault(index) : WholeCheck;

    /// <summary>已放行产出的位置集合：Single 给整节点，PerItem 给全部已发布实例。</summary>
    public override IReadOnlyList<(int Node, int? Item)> ReleasedOutputs()
    {
        if (!Released)
        {
            return [];
        }

        return Mode == NodeMode.PerItem
            ? [.. Items.Select(item => (Index, (int?)item))]
            : [(Index, null)];
    }

    /// <summary>执行节点在快照里的一刻状态。</summary>
    public NodeStateSnapshot StateSnapshot()
    {
        List<int> items = Expanded ? [.. Items] : [];
        int completed;
        if (Expanded)
        {
            completed = items.Count(item => _itemRev.ContainsKey(item));
        }
        else
        {
            completed = Rev > 0 ? 1 : 0;
        }

        NodeState state;
        if (Canceled)
        {
            state = NodeState.Canceled;
        }
        else if (Awaiting)
        {
            state = NodeState.AwaitingApproval;
        }
        else if (Blocked)
        {
            state = NodeState.Blocked;
        }
        else if (Active > 0)
        {
            state = NodeState.Running;
        }
        else if (Expanded)
        {
            state = completed >= items.Count ? NodeState.Done : NodeState.Running;
        }
        else
        {
            state = Rev > 0 ? NodeState.Done : NodeState.Pending;
        }

        return new NodeStateSnapshot(Index, state, items, completed);
    }
}
