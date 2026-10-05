using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;

namespace Kuroe.Workflows.Tasks;

/// <summary>一个执行节点在任务内的运行时对象：执行节点定义与节点自身状态的合体。
/// 节点状态只经推进方法改动，读写都要求持有任务 Gate。</summary>
internal sealed class RuntimeExecutable(ExecutableNode executable) : RuntimeNode(executable)
{
    /// <summary>提交时锁定的执行节点定义，不可变。</summary>
    public ExecutableNode Executable { get; } = executable;

    /// <summary>输入侧消费账本，声明 AnyOf 的节点装配，其余节点为空走原有判定。</summary>
    public NodeInput? Input { get; private set; }

    /// <summary>绑定输入消费账本。</summary>
    public void Bind(NodeInput input) => Input = input;

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

    /// <summary>整节点是否已有发表产出。</summary>
    public bool Published { get; private set; }

    /// <summary>已有发表产出的实例。返工作废后对应项移除。只经 Publish 与 Invalidate 改动。</summary>
    private readonly HashSet<int> _publishedItems = [];

    /// <summary>整节点的已启动 run 数，只经 RecordRun 改动。</summary>
    private int _wholeRuns;

    /// <summary>逐条目的已启动 run 数，只经 RecordRun 改动。</summary>
    private readonly Dictionary<int, int> _itemRuns = [];

    private bool _awaiting;
    private bool _blocked;
    private bool _canceled;
    private bool _rerunRequested;

    public override bool Awaiting => _awaiting;

    /// <summary>停在等待返工或放行。</summary>
    public bool Blocked => _blocked;

    public override bool Canceled => _canceled;

    /// <summary>被作废的重跑目标：下一次启动判定越过就绪条件直接重启一次。</summary>
    public bool RerunRequested => _rerunRequested;

    /// <summary>记下重跑请求，供返工作废后的重启判定使用。</summary>
    public void RequestRerun() => _rerunRequested = true;

    /// <summary>重跑请求已消耗。</summary>
    public void ClearRerun() => _rerunRequested = false;

    /// <summary>产出已放行、可被下游消费：产出已发布且不在等待、阻塞与取消。PerItem 要求全部实例已发布。</summary>
    public override bool Released =>
        (Mode == NodeMode.PerItem
            ? Expanded && Items.All(item => _publishedItems.Contains(item))
            : Published)
        && !Awaiting && !Blocked && !Canceled;

    /// <summary>单调的发布计数，作废归零发表号但不清计数。消费账按它记账，重跑后版本不回退。</summary>
    public override long Revision => _stamp;
    private long _stamp;

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
    public bool Complete(int? item) => item is { } index ? _publishedItems.Contains(index) : Published;

    /// <summary>实例或整节点产出的发表：记录实例并推进单调计数，下游据此重新评估。</summary>
    public void Publish(int? item)
    {
        if (item is { } index)
        {
            _publishedItems.Add(index);
        }
        else
        {
            Published = true;
        }

        _stamp++;
    }

    /// <summary>记一轮派发，次数按实例或整节点累计。调用点唯一：WorkTask.Attach。</summary>
    public void RecordRun(int? item)
    {
        if (item is { } index)
        {
            _itemRuns[index] = _itemRuns.GetValueOrDefault(index) + 1;
        }
        else
        {
            _wholeRuns++;
        }
    }

    /// <summary>某实例或整节点当前已启动的 run 数，尚未启动过为 0。</summary>
    public int ExecutionCount(int? item) =>
        item is { } index ? _itemRuns.GetValueOrDefault(index) : _wholeRuns;

    /// <summary>节点内最高的执行次数：整节点与各条目已启动 run 数的最大值，未执行过为 0。</summary>
    public int MaxExecutionCount => Math.Max(_wholeRuns, _itemRuns.Values.Prepend(0).Max());

    /// <summary>作废实例或整节点的已发表产出，返工起点用它让下游重新等待。PerItem 执行节点不带条目时清空全部实例。</summary>
    public void Invalidate(int? item)
    {
        if (item is { } index)
        {
            _publishedItems.Remove(index);
        }
        else if (Mode == NodeMode.PerItem)
        {
            _publishedItems.Clear();
        }
        else
        {
            Published = false;
        }
    }

    /// <summary>一个已启动的实例终结，在跑数减一。</summary>
    public void ReleaseActive()
    {
        if (Active > 0)
        {
            Active--;
        }
    }

    /// <summary>记下新启动的实例数。</summary>
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

    /// <summary>产出后按门控停在等待批准，Auto 不停。返回是否停留。</summary>
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
    public ExecutableStateSnapshot StateSnapshot()
    {
        List<int> items = Expanded ? [.. Items] : [];
        int completed;
        if (Expanded)
        {
            completed = items.Count(item => _publishedItems.Contains(item));
        }
        else
        {
            completed = Published ? 1 : 0;
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
            state = Published ? NodeState.Done : NodeState.Pending;
        }

        return new ExecutableStateSnapshot(Index, state, items, completed);
    }
}
