using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;
using ContainerNode = Kuroe.Shared.Workflows.Graph.ContainerNode;

namespace Kuroe.Workflows.Tasks;

/// <summary>按图驱动的执行推进：激活、发布、返工与会合。节点状态在运行时节点对象上，这里是图算法面。
/// 容器随成员推进刷新齐备与门控。所有方法都要求调用者持有所属任务的 Gate。</summary>
internal sealed class TaskFlow
{
    private readonly WorkTask _task;
    private readonly RuntimeNode[] _nodes;

    internal TaskFlow(WorkTask task)
    {
        _task = task;
        var nodes = new RuntimeNode[task.Graph.Nodes.Count];
        int i = 0;
        foreach (GraphNode node in task.Graph.Nodes)
        {
            nodes[i++] = node switch
            {
                ExecutableNode executable => new RuntimeExecutable(executable),
                ContainerNode container => new RuntimeContainer(container, index => nodes[index]),
                _ => throw new InvalidOperationException($"不支持的节点类型：{node.GetType().Name}"),
            };
        }

        _nodes = nodes;
    }

    /// <summary>任务锁定的流程编译视图。</summary>
    public NodeGraph Graph => _task.Graph;

    /// <summary>图上的运行时节点对象，按先根序与全节点一一对应。读写要求持有 Gate。</summary>
    public IReadOnlyList<RuntimeNode> Nodes => _nodes;

    /// <summary>图上的运行时执行节点对象，只含会派发 run 的节点。</summary>
    public IReadOnlyList<RuntimeExecutable> Executables => [.. _nodes.OfType<RuntimeExecutable>()];

    /// <summary>按序号取运行时节点对象。</summary>
    public RuntimeNode this[int index] => _nodes[index];

    /// <summary>按序号取运行时执行节点对象，序号必须是执行节点。</summary>
    public RuntimeExecutable Executable(int index) => (RuntimeExecutable)_nodes[index];

    // ---- 实例展开 ----

    /// <summary>PerItem 执行节点的实例条目集。拆分来源给分支过滤后的条目，否则取对齐来源的并集。尚未可用时为空。</summary>
    private List<int> ResolveItems(RuntimeExecutable node)
    {
        if (node.Expanded)
        {
            return [.. node.Items];
        }

        ExecutableNode executable = node.Executable;
        List<int> items = [];
        if (Graph.ItemSource(node.Index) is { } plan && _task.SplitFor(plan) is { } output)
        {
            items = [.. output.Items
                .Where(item => executable.Branch is null || item.Branch == executable.Branch)
                .Select(item => item.Index)];
        }
        else
        {
            foreach (FlowEdge edge in Graph.Incoming(node.Index))
            {
                if (edge.Feed == EdgeFeed.Aligned && Executable(edge.From).Expanded)
                {
                    items.AddRange(Executable(edge.From).Items);
                }
            }

            items = [.. items.Distinct()];
        }

        if (items.Count > 0)
        {
            node.AdoptItems(items);
        }

        return items;
    }

    // ---- 就绪判定 ----

    /// <summary>一条入边当前是否可消费：来源产出的已放行部分里有目标要的那份。整份、整集与容器来源一视同仁。</summary>
    private bool FeedSatisfied(FlowEdge edge, int? item)
    {
        RuntimeNode source = this[edge.From];
        switch (edge.Feed)
        {
            case EdgeFeed.Single:
            case EdgeFeed.AllInstances:
                return source.Released;

            case EdgeFeed.Items:
                return _task.SplitFor(edge.From) is not null;

            case EdgeFeed.Aligned:
                if (item is not { } index)
                {
                    return true;
                }

                RuntimeExecutable aligned = (RuntimeExecutable)source;
                if (!aligned.Expanded)
                {
                    return false;
                }

                // 已展开且承担该条目才需要它的产出
                return !aligned.HasItem(index) || aligned.Complete(index);

            default:
                return false;
        }
    }

    private bool ItemSatisfied(RuntimeExecutable node, int? item) =>
        Graph.Incoming(node.Index).All(edge => FeedSatisfied(edge, item));

    /// <summary>执行节点是否还有待启动的实例。Single 执行节点一次执行，PerItem 执行节点按实例逐步启动。</summary>
    public bool CanStart(RuntimeExecutable node)
    {
        if (node.HasActive || node.Awaiting || node.Blocked || node.Canceled)
        {
            return false;
        }

        if (node.Mode != NodeMode.PerItem)
        {
            return !node.Complete(null) && ItemSatisfied(node, null);
        }

        return ResolveItems(node).Any(item => !node.Complete(item) && ItemSatisfied(node, item));
    }
    /// <summary>把执行节点可启动的实例取出来启动。调用者应先用 <see cref="CanStart"/> 判定。</summary>
    public IReadOnlyList<RunStarter> Start(RuntimeExecutable node)
    {
        List<RunStarter> starters = [];
        if (node.Mode == NodeMode.PerItem)
        {
            foreach (int item in ResolveItems(node).Where(item => !node.Complete(item) && ItemSatisfied(node, item)))
            {
                node.RecordExecution(item);
                starters.Add(new RunStarter(item));
            }
        }
        else if (!node.Complete(null) && ItemSatisfied(node, null))
        {
            node.RecordExecution(null);
            starters.Add(new RunStarter(null));
        }

        if (starters.Count > 0)
        {
            node.AddActive(starters.Count);
            node.ClearAwaiting();
        }

        return starters;
    }

    /// <summary>一个待派发的实例：条目序可为空（整节点）。</summary>
    public readonly record struct RunStarter(int? Item);

    // ---- 发布与作废 ----

    /// <summary>实例或整节点产出的发表：发表号递增，下游据此重新评估。</summary>
    public static void Publish(RuntimeExecutable node, int? item) => node.Publish(item);

    /// <summary>作废实例或整节点的已发表产出并刷新祖先容器，返工起点用它让下游重新等待。PerItem 执行节点不带条目时清空全部实例。</summary>
    private void InvalidateNode(RuntimeExecutable node, int? item)
    {
        node.Invalidate(item);
        RefreshContainers(node.Index);
    }

    /// <summary>作废实例或整节点的已发表产出，宿主返工入口按序号定位节点。</summary>
    public void Invalidate(int node, int? item) => InvalidateNode(Executable(node), item);

    // ---- 返工 ----

    /// <summary>检查节点返工要退回的实施来源。</summary>
    public IReadOnlyList<int> ReworkTargets(RuntimeExecutable checkNode) => Graph.CheckedSources(checkNode.Index);

    /// <summary>逐条返工要退回的实施来源：只退回实际承担该条目的执行节点。</summary>
    public IReadOnlyList<int> ReworkTargets(RuntimeExecutable checkNode, int item) =>
        [.. Graph.CheckedSources(checkNode.Index).Where(source => Executable(source).HasItem(item))];

    /// <summary>顺着出边要通知的下游执行节点，去重保持顺序。</summary>
    public IReadOnlyList<int> Downstream(RuntimeNode node) =>
        [.. Graph.Outgoing(node.Index).Select(edge => edge.To).Distinct()];

    // ---- 容器推进 ----

    /// <summary>沿着执行节点所在的祖先链刷新容器：成员产出已放行时置齐备位并按门控停留或广播出边，
    /// 成员停驻或作废时复位齐备份并撤容器等待，不齐备的祖先一并复位。返回本次新齐备容器的出边目标。</summary>
    public IReadOnlyList<int> RefreshContainers(int executableIndex)
    {
        List<int> notify = [];
        int nodeIndex = executableIndex;
        while (Graph.ContainerOf(nodeIndex) is { } parent && this[parent.Index] is RuntimeContainer container)
        {
            if (!container.Released)
            {
                container.ResetProduced();
                container.ClearAwaiting();
                nodeIndex = container.Index;
                continue;
            }

            if (container.Gate == NodeGate.Review)
            {
                if (!container.Produced)
                {
                    container.MarkProduced();
                    container.Park();
                }

                nodeIndex = container.Index;
                continue;
            }

            bool first = !container.Produced;
            container.MarkProduced();
            if (first)
            {
                notify.AddRange(Downstream(container));
            }

            nodeIndex = container.Index;
        }

        return notify;
    }

    // ---- 汇总 ----

    /// <summary>是否有执行节点或容器停在等人批准。</summary>
    public bool HasAwaiting => _nodes.Any(node => node.Awaiting);

    /// <summary>是否有执行节点停在等人返工。</summary>
    public bool HasBlocked => _nodes.OfType<RuntimeExecutable>().Any(node => node.Blocked);

    /// <summary>还有在跑的 run 或可启动的执行节点，任务就没走完。</summary>
    public bool HasWork => _nodes.OfType<RuntimeExecutable>().Any(node => node.HasActive || CanStart(node));

    /// <summary>正在等人批准的执行节点。</summary>
    public IReadOnlyList<int> AwaitingNodes =>
        [.. _nodes.OfType<RuntimeExecutable>().Where(node => node.Awaiting).Select(node => node.Index)];

    /// <summary>正在等人批准的容器。</summary>
    public IReadOnlyList<int> AwaitingContainers =>
        [.. _nodes.OfType<RuntimeContainer>().Where(container => container.Awaiting).Select(container => container.Index)];

    /// <summary>放行等待批准的容器：撤等待、收集容器出边目标，并沿祖先链刷新出随放行新齐备的广播。批准信号落地时调用。</summary>
    public IReadOnlyList<int> ReleaseContainers()
    {
        List<int> outlets = [];
        foreach (RuntimeContainer container in _nodes.OfType<RuntimeContainer>())
        {
            if (!container.Awaiting)
            {
                continue;
            }

            container.ClearAwaiting();
            outlets.AddRange(Downstream(container));
            outlets.AddRange(RefreshContainers(FirstExecutableIn(container)));
        }

        return [.. outlets.Distinct()];
    }

    /// <summary>容器内一个执行节点，作为祖先链刷新的起点。容器内执行节点非空由提交时的 WorkflowRules 校验保证。</summary>
    private int FirstExecutableIn(RuntimeContainer container) => Graph.ExecutablesIn(container.Index)[0];

    /// <summary>取消任务：全部节点进入取消态。</summary>
    public void Cancel()
    {
        foreach (RuntimeNode node in _nodes)
        {
            node.Cancel();
        }
    }
    // ---- run 收口 ----

    /// <summary>run 收口的决策结果：要通知的执行节点，或自动返工重派的实例。</summary>
    public sealed record SettlePlan(IReadOnlyList<int> Notify, IReadOnlyList<(int Node, int? Item)> Rerun);

    /// <summary>run 收口：失败与未收口先停驻阻塞，通过后记产出、处理门控与检查结论，
    /// 产出出向下游的通知或重派计划。接收器先经 ReleaseActive 再进入。</summary>
    public SettlePlan Settle(Run run)
    {
        RuntimeExecutable node = Executable(run.Context.NodeIndex);
        int? item = run.Context.ItemIndex;

        if (run.State != RunState.Succeeded)
        {
            node.EnterBlocked([(node.Index, item)]);
            return new SettlePlan([], []);
        }

        ExecutableNode executable = node.Executable;
        if (executable.Output == NodeOutput.Plan && _task.SplitFor(node.Index)?.Origin != run.Id)
        {
            run.MarkUncollected("规划执行节点没有交回条目拆分，本步未收口。");
            node.EnterBlocked([(node.Index, null)]);
            return new SettlePlan([], []);
        }

        if (executable.Output == NodeOutput.Review && node.LastCheck(item)?.Origin != run.Id)
        {
            run.MarkUncollected("检查执行节点没有交回结论，本步未收口。");
            node.EnterBlocked([(node.Index, item)]);
            return new SettlePlan([], []);
        }

        if (executable.Output == NodeOutput.Plan)
        {
            // 拆分已由回执写入 _splits
            Publish(node, null);

            return Settled(node, node.Park(), Downstream(node));
        }

        if (executable.Output == NodeOutput.Review)
        {
            CheckResult? check = node.LastCheck(item);
            if (check is { Passed: true })
            {
                Publish(node, item);

                return Settled(node, node.Park(), Downstream(node));
            }

            if (Retryable(executable, check))
            {
                List<(int Node, int? Item)> rerun = [];
                foreach (int source in item is { } index ? ReworkTargets(node, index) : ReworkTargets(node))
                {
                    InvalidateNode(Executable(source), item);
                    rerun.Add((source, item));
                }

                return new SettlePlan([], rerun);
            }

            IReadOnlyList<int> sources = item is { } failed ? ReworkTargets(node, failed) : ReworkTargets(node);
            node.EnterBlocked([.. sources.Select(source => (source, item))]);

            return new SettlePlan([], []);
        }

        Publish(node, item);

        return Settled(node, node.Park(), Downstream(node));
    }

    /// <summary>产出发布后的共同收口：把祖先容器临近齐备的出边目标并入通知。</summary>
    private SettlePlan Settled(RuntimeExecutable node, bool parked, IReadOnlyList<int> downstream)
    {
        IReadOnlyList<int> containers = RefreshContainers(node.Index);

        return parked ? new SettlePlan(containers, []) : new SettlePlan([.. downstream, .. containers], []);
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

    // ---- 宿主入口 ----

    /// <summary>一个执行节点被激活后的评估：静态拆分就地产出、等待批准的放行、输入齐备启动实例。
    /// 返回要通知的下游与要派发的实例。要求持有 Gate。</summary>
    public EvaluateResult Evaluate(RuntimeExecutable node)
    {
        ExecutableNode executable = node.Executable;
        if (executable.IsStaticSplit && _task.SplitFor(node.Index) is null)
        {
            // 纯静态拆分不派 run，激活即产出
            _task.SetSplit(node.Index, new PlanOutput(new RunId(0), SplitMerge.Apply(executable.Split!, []).Value));
            Publish(node, null);

            return new EvaluateResult(Settled(node, false, Downstream(node)).Notify, []);
        }

        if (node.ClearAwaiting())
        {
            return new EvaluateResult(
                [.. Downstream(node).Concat(RefreshContainers(node.Index)).Distinct()], []);
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
        [.. _nodes.OfType<RuntimeExecutable>()
            .Where(node => Graph.Incoming(node.Index).Count == 0)
            .Select(node => node.Index)];

    /// <summary>被阻塞的执行节点，等人返工或放行。</summary>
    public IReadOnlyList<int> BlockedNodes =>
        [.. _nodes.OfType<RuntimeExecutable>().Where(node => node.Blocked).Select(node => node.Index)];

    // ---- 容器快照 ----

    /// <summary>各容器在快照里的一刻状态：成员产出放行情况与门控停留。</summary>
    public IReadOnlyList<ContainerSnapshot> ContainerSnapshots()
    {
        List<ContainerSnapshot> snapshots = [];
        foreach (RuntimeContainer container in _nodes.OfType<RuntimeContainer>())
        {
            bool released = container.Released;
            NodeState state;
            if (container.Canceled)
            {
                state = NodeState.Canceled;
            }
            else if (container.Awaiting)
            {
                state = NodeState.AwaitingApproval;
            }
            else if (released)
            {
                state = NodeState.Done;
            }
            else
            {
                state = Graph.ExecutablesIn(container.Index).Any(executable =>
                {
                    RuntimeExecutable item = Executable(executable);
                    return item.Active > 0 || item.Complete(null) || item.Expanded;
                }) ? NodeState.Running : NodeState.Pending;
            }

            snapshots.Add(new ContainerSnapshot(container.Index, container.Name.Value, container.Container.Path, state, container.Container.Members));
        }

        return snapshots;
    }

    /// <summary>计算返工会作废并重跑的目标：各阻塞节点记录的目标按条目过滤，配对其阻塞节点。不改变状态。要求持有 Gate。</summary>
    public IReadOnlyList<(int Blocked, int Node, int? Item)> PlanRework(int? itemIndex)
    {
        List<(int Blocked, int Node, int? Item)> targets = [];
        foreach (int blocked in BlockedNodes)
        {
            foreach ((int node, int? item) in Executable(blocked).ReworkTargets)
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
    public void Unblock(int node, IReadOnlyList<(int Node, int? Item)> handled) =>
        Executable(node).FinishRework(handled);

    /// <summary>实施执行节点要读的返工意见：引用它的检查执行节点最近一次拒绝结论。检查执行节点读自己的结论。要求持有 Gate。</summary>
    public CheckResult? ReworkFor(RuntimeExecutable executable, int? itemIndex)
    {
        if (executable.Output == NodeOutput.Review)
        {
            CheckResult? check = executable.LastCheck(itemIndex);

            return check is { Passed: false } ? check : null;
        }

        foreach (int check in Graph.ExecutableNodes
                     .Where(candidate => candidate.Output == NodeOutput.Review)
                     .Select(candidate => candidate.Index))
        {
            if (!Graph.CheckedSources(check).Contains(executable.Index))
            {
                continue;
            }

            RuntimeExecutable checkNode = Executable(check);
            CheckResult? error = itemIndex is { } index
                ? checkNode.LastCheck(index) ?? checkNode.LastCheck(null)
                : checkNode.LastCheck(null);
            if (error is { Passed: false })
            {
                return error;
            }
        }

        return null;
    }
}

