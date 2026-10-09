using System.Text.RegularExpressions;
using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;
using ContainerNode = Kuroe.Shared.Workflows.Graph.ContainerNode;

namespace Kuroe.Workflows.Tasks;

/// <summary>按图驱动的执行推进：激活、发布、返工与会合。节点状态在运行时节点对象上，这里是图算法面。
/// 容器随成员推进刷新齐备与门控。所有方法都要求调用者持有所属任务的 Gate。</summary>
internal sealed class TaskRuntime
{
    private readonly WorkTask _task;
    private readonly RuntimeNode?[] _nodes;
    private bool _taskCanceled;

    internal TaskRuntime(WorkTask task)
    {
        _task = task;
        _nodes = new RuntimeNode?[task.Graph.Nodes.Count];
    }

    /// <summary>任务锁定的流程编译视图。</summary>
    public NodeGraph Graph => _task.Graph;

    /// <summary>已实例化的运行时节点数，诊断与测试观测懒实例化的边界。</summary>
    internal int MaterializedCount => _nodes.Count(node => node is not null);

    /// <summary>按序号取运行时节点对象，未实例化时按图定义实例化。读写要求持有 Gate。</summary>
    public RuntimeNode this[int index] => Ensure(index);

    /// <summary>按序号取运行时执行节点对象，序号必须是执行节点。</summary>
    public RuntimeExecutable Executable(int index) => (RuntimeExecutable)Ensure(index);

    /// <summary>序号对应的运行时节点对象，尚未实例化时按图定义创建，创建后若任务已取消直接落在取消态。</summary>
    private RuntimeNode Ensure(int index)
    {
        if (_nodes[index] is { } existing)
        {
            return existing;
        }

        RuntimeNode created = Graph.Nodes[index] switch
        {
            ExecutableNode executable => new RuntimeExecutable(executable),
            ContainerNode container => new RuntimeContainer(container, Ensure),
            _ => throw new InvalidOperationException($"不支持的节点类型：{Graph.Nodes[index].GetType().Name}"),
        };

        if (_taskCanceled)
        {
            created.Cancel();
        }

        // 先入表再绑账本，来源解析对互引的节点返回已入表对象，不会递归
        _nodes[index] = created;

        if (created is RuntimeExecutable executableNode && ShouldBind(executableNode.Executable))
        {
            executableNode.Bind(NodeInput.Build(_task, executableNode, Ensure));
        }

        return created;
    }

    /// <summary>有入边的 Single 执行节点装配输入消费账本：来源齐备且发布新版本才启动。
    /// 输入节点不启动 run，无入边节点与 PerItem 执行节点不经账本。</summary>
    private bool ShouldBind(ExecutableNode executable) =>
        executable.Execution.Output != NodeOutput.Input
        && executable.Execution.Mode != NodeMode.PerItem
        && Graph.Incoming(executable.Index).Count > 0;

    /// <summary>执行节点在快照里的一刻状态，未实例化的执行节点按默认待办报告。</summary>
    public ExecutableStateSnapshot SnapshotState(int index) =>
        _nodes[index] is RuntimeExecutable node
            ? node.StateSnapshot()
            : new ExecutableStateSnapshot(index, NodeState.Pending, [], 0, [], null);

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
                .Where(item => executable.Execution.Branch is null || item.Branch == executable.Execution.Branch)
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

    /// <summary>执行节点是否还有待启动的实例。账本节点随来源新版本整节点重启，
    /// 无入边节点发表即终态，PerItem 执行节点按实例逐步启动。</summary>
    public bool CanStart(RuntimeExecutable node)
    {
        if (node.IsInputOutput || node.HasActive || node.Awaiting || node.Blocked || node.Canceled)
        {
            return false;
        }

        if (node.Input is not null)
        {
            return (node.Input.Ready || node.RerunRequested) && !AtLimit(node, null);
        }

        return CanStartWhole(node);
    }

    /// <summary>无账本执行节点的启动判定：PerItem 按实例，整节点未发表则启动。</summary>
    private bool CanStartWhole(RuntimeExecutable node) =>
        node.Mode == NodeMode.PerItem
            ? ResolveItems(node).Any(item => !node.Complete(item) && ItemSatisfied(node, item) && !AtLimit(node, item))
            : !node.Complete(null) && !AtLimit(node, null);

    /// <summary>该执行路径已达执行次数上限，达到后不再启动新 run。</summary>
    private static bool AtLimit(RuntimeExecutable node, int? item) =>
        node.ExecutionCount(item) >= node.Executable.MaxRuns;

    /// <summary>把执行节点可启动的实例取出来启动。调用者应先用 <see cref="CanStart"/> 判定。</summary>
    public IReadOnlyList<RunStarter> Start(RuntimeExecutable node)
    {
        List<RunStarter> starters = [];
        if (node.Input is not null)
        {
            if ((node.Input.Ready || node.RerunRequested) && !AtLimit(node, null))
            {
                starters.Add(new RunStarter(null));
            }
        }
        else
        {
            starters.AddRange(StartWhole(node));
        }

        if (starters.Count > 0)
        {
            node.AddActive(starters.Count);
            node.ClearAllAwaiting();
            node.ClearRerun();
            node.Input?.Consume();
        }

        return starters;
    }

    /// <summary>无账本执行节点的可启动实例：PerItem 按条目，整节点未发表给一个整节点实例。</summary>
    private IReadOnlyList<RunStarter> StartWhole(RuntimeExecutable node)
    {
        if (node.Mode == NodeMode.PerItem)
        {
            return [.. ResolveItems(node)
                .Where(item => !node.Complete(item) && ItemSatisfied(node, item) && !AtLimit(node, item))
                .Select(item => new RunStarter(item))];
        }

        return !node.Complete(null) && !AtLimit(node, null) ? [new RunStarter(null)] : [];
    }

    /// <summary>一个待启动的实例：条目序可为空（整节点）。</summary>
    public readonly record struct RunStarter(int? Item);

    // ---- 发布与作废 ----

    /// <summary>实例或整节点产出的发表：发表号递增，下游据此重新评估。</summary>
    public static void Publish(RuntimeExecutable node, int? item) => node.Publish(item);

    /// <summary>作废实例或整节点的已发表产出并刷新祖先容器，返工起点用它让下游重新等待。PerItem 执行节点不带条目时清空全部实例。</summary>
    private void InvalidateNode(RuntimeExecutable node, int? item)
    {
        // 账本语义的节点没有“未完成”这一状态可回退，作废即记下一次强制重启
        if (node.Input is not null)
        {
            node.RequestRerun();
        }

        node.Invalidate(item);
        if (node.Executable.HasOutputPorts)
        {
            _task.ClearPortValues(node.Index);
        }

        RefreshContainers(node.Index);
    }

    /// <summary>作废实例或整节点的已发表产出，宿主返工入口按序号定位节点。</summary>
    public void Invalidate(int node, int? item) => InvalidateNode(Executable(node), item);

    // ---- 返工 ----

    /// <summary>顺着出边要通知的下游执行节点，去重保持顺序。</summary>
    public IReadOnlyList<int> Downstream(RuntimeNode node) =>
        [.. Graph.Outgoing(node.Index).Select(edge => edge.To).Distinct()];

    // ---- 容器推进 ----

    /// <summary>沿着执行节点所在的祖先链刷新容器：来源版本账有变化且内容齐备时推进内容版本，
    /// 按门控停留待批准或广播出边；内容未齐备时撤容器等待。返回内容版本变化的 Auto 容器出边目标。</summary>
    public IReadOnlyList<int> RefreshContainers(int executableIndex)
    {
        List<int> notify = [];
        int nodeIndex = executableIndex;
        while (Graph.ContainerOf(nodeIndex) is { } parent && Ensure(parent.Index) is RuntimeContainer container)
        {
            if (!container.ContentReady)
            {
                container.ClearAwaiting();
                nodeIndex = container.Index;
                continue;
            }

            bool changed = container.PublishIfChanged(SourceRevisions(container.Index));
            if (container.Gate == NodeGate.Review)
            {
                if (changed)
                {
                    container.Park();
                }
            }
            else if (changed)
            {
                notify.AddRange(Downstream(container));
            }

            nodeIndex = container.Index;
        }

        return notify;
    }

    /// <summary>容器直接来源的版本快照：直接成员与子容器逐个取运行时版本。</summary>
    private Dictionary<int, long> SourceRevisions(int containerIndex)
    {
        ContainerNode container = (ContainerNode)Graph[containerIndex];
        Dictionary<int, long> sources = [];
        foreach (int source in container.Members.Concat(container.SubContainers))
        {
            sources[source] = Ensure(source).Revision;
        }

        return sources;
    }

    // ---- 汇总 ----

    /// <summary>是否有执行节点或容器停在等待批准。未实例化的节点不可能在等待。</summary>
    public bool HasAwaiting => _nodes.Any(node => node is { Awaiting: true });

    /// <summary>是否有执行节点停在等待返工。</summary>
    public bool HasBlocked => _nodes.OfType<RuntimeExecutable>().Any(node => node.Blocked);

    /// <summary>还有在跑的 run 或可启动的执行节点，任务就没走完。可启动的执行节点依赖齐备时已被通知并实例化，未实例化的不可启动。</summary>
    public bool HasWork => _nodes.OfType<RuntimeExecutable>().Any(node => node.HasActive || CanStart(node));

    /// <summary>正在等待批准的容器。</summary>
    public IReadOnlyList<int> AwaitingContainers =>
        [.. _nodes.OfType<RuntimeContainer>().Where(container => container.Awaiting).Select(container => container.Index)];

    /// <summary>全部等待批准的产出 run。</summary>
    public IReadOnlyList<RunId> AwaitingRuns =>
        [.. _nodes.OfType<RuntimeExecutable>().SelectMany(node => node.AwaitingRuns)];

    /// <summary>给定 run 中此刻在等待批准的数量。</summary>
    public int CountAwaitingRuns(IReadOnlyList<RunId> runs)
    {
        HashSet<RunId> awaiting = [.. AwaitingRuns];

        return runs.Count(awaiting.Contains);
    }

    /// <summary>批准一批产出 run：清掉各自等待，节点等待清空后放行下游并刷新容器。
    /// runs 为空时批准全部等待。返回要通知的执行节点。要求持有 Gate。</summary>
    public IReadOnlyList<int> ApproveRuns(IReadOnlyList<RunId> runs)
    {
        List<RuntimeExecutable> released = [];
        if (runs.Count == 0)
        {
            foreach (RuntimeExecutable node in _nodes.OfType<RuntimeExecutable>())
            {
                if (node.AwaitingRuns.Count == 0)
                {
                    continue;
                }

                node.ClearAllAwaiting();
                released.Add(node);
            }
        }
        else
        {
            foreach (RunId run in runs)
            {
                if (_nodes.OfType<RuntimeExecutable>().FirstOrDefault(node => node.AwaitingRuns.Contains(run)) is not { } owner)
                {
                    continue;
                }

                owner.ClearAwaiting(run);
                if (!owner.Awaiting)
                {
                    released.Add(owner);
                }
            }
        }

        List<int> notify = [];
        foreach (RuntimeExecutable node in released.Distinct())
        {
            notify.AddRange(Downstream(node));
            notify.AddRange(RefreshContainers(node.Index));
        }

        return [.. notify.Distinct()];
    }

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

    /// <summary>容器内一个执行节点，作为祖先链刷新的起点。容器内执行节点非空由提交时的 FlowRules 校验保证。</summary>
    private int FirstExecutableIn(RuntimeContainer container) => Graph.ExecutablesIn(container.Index)[0];

    /// <summary>取消任务：置任务取消位，已实例化的节点全部进入取消态，迟实例化的节点创建即取消。</summary>
    public void Cancel()
    {
        _taskCanceled = true;
        foreach (RuntimeNode node in _nodes.OfType<RuntimeNode>())
        {
            node.Cancel();
        }
    }

    // ---- run 收口 ----

    /// <summary>run 收口的决策结果：要通知的执行节点，或自动返工重派的实例。</summary>
    public sealed record SettlePlan(IReadOnlyList<int> Notify, IReadOnlyList<(int Node, int? Item)> Rerun);

    /// <summary>run 收口：失败与未收口先停驻阻塞，通过后发布产出并处理门控，
    /// 产出出向下游的通知或重派计划。接收器先经 ReleaseActive 再进入。</summary>
    public SettlePlan Settle(Run run)
    {
        RuntimeExecutable node = Executable(run.Context.NodeIndex);
        int? item = run.Context.ItemIndex;

        if (run.State != RunState.Succeeded)
        {
            _task.NotifyRunSettled(run);
            node.EnterBlocked([(node.Index, item)]);
            NoteLimitReached(node, item);
            return new SettlePlan([], []);
        }

        ExecutableNode executable = node.Executable;
        if (executable.Execution.Output == NodeOutput.Plan && _task.SplitFor(node.Index)?.Origin != run.Id)
        {
            run.MarkUncollected("规划执行节点没有交回条目拆分，本步未收口。");
            node.EnterBlocked([(node.Index, null)]);
            NoteLimitReached(node, null);
            return new SettlePlan([], []);
        }

        if (executable.HasOutputPorts && !HasAllPortValues(executable, _task.PortValuesFor(node.Index)))
        {
            run.MarkUncollected("输出端口内容未交回，本步未收口。");
            node.EnterBlocked([(node.Index, null)]);
            NoteLimitReached(node, null);
            return new SettlePlan([], []);
        }

        // 输出校验不通过即不发布，节点停驻等返工
        if (!PassesValidation(executable.Execution.Validate, run.Result))
        {
            node.EnterBlocked([(node.Index, item)]);
            NoteLimitReached(node, item);
            return new SettlePlan([], []);
        }

        // 拆分已由回执写入 _splits，其余产出直接发布
        Publish(node, item);
        _task.NotifyRunSettled(run);

        return Settled(node, node.Park(run.Id), Downstream(node));
    }

    /// <summary>输出校验判定：不改动任何状态，只给出模型产出是否合格。空产出只有 NonEmpty 判定接受。</summary>
    private static bool PassesValidation(OutputValidation? validation, string? result)
    {
        if (validation is null)
        {
            return true;
        }

        return validation.Predicate switch
        {
            ValidationPredicate.NonEmpty => result is { Length: > 0 },
            ValidationPredicate.TextContains => Contains(result, validation.Argument),
            ValidationPredicate.TextNot => result is { Length: > 0 } && !Contains(result, validation.Argument),
            ValidationPredicate.TextEquals => string.Equals(result, validation.Argument, StringComparison.Ordinal),
            ValidationPredicate.Pattern => Matches(result, validation.Argument),
            _ => true,
        };
    }

    /// <summary>输出端口的声明是否都已交回内容。</summary>
    private static bool HasAllPortValues(ExecutableNode executable, IReadOnlyDictionary<PortName, string>? values) =>
        values is { Count: > 0 } && executable.Outputs.All(port => values.ContainsKey(port));

    private static bool Contains(string? text, string? needle) =>
        needle is { Length: > 0 } && text is not null && text.Contains(needle, StringComparison.Ordinal);

    /// <summary>正则匹配：产物为空或匹配超时一律视为不通过。</summary>
    private static bool Matches(string? text, string? pattern)
    {
        if (text is null || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>失败停驻的路径已达执行上限时记录提示，后续返工不会放行。</summary>
    private void NoteLimitReached(RuntimeExecutable node, int? item)
    {
        if (!AtLimit(node, item))
        {
            return;
        }

        _task.Journal.Append(new ErrorEntry($"节点 {node.Name} 的执行次数已达上限 {node.Executable.MaxRuns} 次，不再重跑。"));
    }

    /// <summary>产出发布后的共同收口：把祖先容器临近齐备的出边目标并入通知。</summary>
    private SettlePlan Settled(RuntimeExecutable node, bool parked, IReadOnlyList<int> downstream)
    {
        IReadOnlyList<int> containers = RefreshContainers(node.Index);

        return parked ? new SettlePlan(containers, []) : new SettlePlan([.. downstream, .. containers], []);
    }

    // ---- 宿主入口 ----

    /// <summary>是否有输入节点停在等待回答。</summary>
    public bool HasAwaitingInput => _nodes.OfType<RuntimeExecutable>().Any(node => node.AwaitingInput);

    /// <summary>回答一个停在等待的输入节点，回答即产出并放行下游。已随任务取消的节点不再接受回答。未指定节点名时回答唯一等待节点。要求持有 Gate。</summary>
    public ErrorOr<List<int>> Answer(string? nodeName, string text)
    {
        List<RuntimeExecutable> awaiting = [.. _nodes.OfType<RuntimeExecutable>().Where(node => node.AwaitingInput && !node.Canceled)];

        RuntimeExecutable? target;
        if (nodeName is null)
        {
            target = awaiting.Count == 1 ? awaiting[0] : null;
        }
        else
        {
            target = awaiting.FirstOrDefault(node => node.Name.Value == nodeName);
        }

        if (target is null)
        {
            if (nodeName is null)
            {
                if (awaiting.Count == 0)
                {
                    return [TaskErrors.NoAwaitingInput(_task.Id)];
                }

                return [TaskErrors.AmbiguousInput(_task.Id, awaiting.Count)];
            }

            return [TaskErrors.NoAwaitingInputNode(nodeName)];
        }

        target.Answer(text);

        List<int> notify = [.. Downstream(target), .. RefreshContainers(target.Index)];

        List<int> notified = [.. notify.Distinct()];

        return notified;
    }

    /// <summary>一个执行节点被激活后的评估：静态拆分就地产出、放行待批准的节点、输入齐备启动实例。
    /// 返回要通知的下游与要启动的实例。要求持有 Gate。</summary>
    public EvaluateResult Evaluate(RuntimeExecutable node)
    {
        ExecutableNode executable = node.Executable;
        if (node.IsInputOutput)
        {
            // 输入节点每次被激活即复位已答内容并重新挂起等待回答，与来源反馈或启动评估相同
            node.ResetInput();
            node.ParkInput();

            return new EvaluateResult([], []);
        }

        if (executable.IsStaticSplit && _task.SplitFor(node.Index) is null)
        {
            // 纯静态拆分不启动 run，激活即产出
            _task.SetSplit(node.Index, new PlanOutput(new RunId(0), SplitMerge.Apply(executable.Execution.Split!, []).Value));
            Publish(node, null);

            return new EvaluateResult(Settled(node, false, Downstream(node)).Notify, []);
        }

        if (CanStart(node))
        {
            return new EvaluateResult([], Start(node));
        }

        return new EvaluateResult([], []);
    }

    /// <summary>一次评估的结果：要通知的下游执行节点与要启动的实例。</summary>
    public readonly record struct EvaluateResult(IReadOnlyList<int> Notify, IReadOnlyList<RunStarter> Starters);

    /// <summary>任务启动的第一批，返回前全部实例化：无入边的执行节点与全部输入节点。</summary>
    public IReadOnlyList<int> Roots() =>
        [.. Graph.StartCandidates().Select(node => Ensure(node.Index).Index)];

    /// <summary>被阻塞的执行节点，等待返工或放行。</summary>
    public IReadOnlyList<int> BlockedNodes =>
        [.. _nodes.OfType<RuntimeExecutable>().Where(node => node.Blocked).Select(node => node.Index)];

    // ---- 容器快照 ----

    /// <summary>各容器在快照里的一刻状态：成员产出放行情况与门控停留。快照不实例化节点。</summary>
    public IReadOnlyList<ContainerSnapshot> ContainerSnapshots()
    {
        List<ContainerSnapshot> snapshots = [];
        foreach (ContainerNode container in Graph.Containers)
        {
            snapshots.Add(_nodes[container.Index] is RuntimeContainer runtime
                ? ContainerSnapshotOf(runtime)
                : UnmaterializedSnapshotOf(container));
        }

        return snapshots;
    }

    /// <summary>未实例化容器的一刻状态：没有门控停留与产出，状态全由已实例化的成员活动决定。</summary>
    private ContainerSnapshot UnmaterializedSnapshotOf(ContainerNode container) =>
        new(container.Index, container.Name.Value, container.Path, ContainerActivityState(container), container.Members);

    /// <summary>已实例化容器的一刻状态。成员状态只在已实例化成员上读，未实例化成员视为没有活动，快照不实例化节点。</summary>
    private ContainerSnapshot ContainerSnapshotOf(RuntimeContainer container)
    {
        NodeState state;
        if (container.Canceled)
        {
            state = NodeState.Canceled;
        }
        else if (container.Awaiting)
        {
            state = NodeState.AwaitingApproval;
        }
        else if (ContainerReleased(container))
        {
            state = NodeState.Done;
        }
        else
        {
            state = ContainerActivityState(container.Container);
        }

        return new ContainerSnapshot(container.Index, container.Name.Value, container.Container.Path, state, container.Container.Members);
    }

    /// <summary>容器里已实例化成员的活动状态：有在跑或已展开则运行中，停在等回答则待输入，否则待办。</summary>
    private NodeState ContainerActivityState(ContainerNode container)
    {
        if (Graph.ExecutablesIn(container.Index).Any(MemberActive))
        {
            return NodeState.Running;
        }

        return Graph.ExecutablesIn(container.Index).Any(MemberAwaitingInput)
            ? NodeState.AwaitingInput
            : NodeState.Pending;
    }

    /// <summary>容器产出已放行：自身不等待不取消，直接成员与子容器都已放行。未实例化的成员视为未放行。</summary>
    private bool ContainerReleased(RuntimeContainer container)
    {
        if (container.Awaiting || container.Canceled)
        {
            return false;
        }

        foreach (int member in container.Container.Members)
        {
            if (_nodes[member] is not RuntimeExecutable node || !node.Released)
            {
                return false;
            }
        }

        foreach (int child in container.Container.SubContainers)
        {
            if (_nodes[child] is not RuntimeContainer childContainer || !ContainerReleased(childContainer))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>成员执行节点是否有在跑或已展开的实例，未实例化的成员没有活动。</summary>
    private bool MemberActive(int executableIndex) =>
        _nodes[executableIndex] is RuntimeExecutable node
            && (node.Active > 0 || node.Complete(null) || node.Expanded);

    /// <summary>成员执行节点是否停在等待回答，未实例化的成员不在等待。</summary>
    private bool MemberAwaitingInput(int executableIndex) =>
        _nodes[executableIndex] is RuntimeExecutable node && node.AwaitingInput;

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

                if (AtLimit(Executable(node), item))
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
}

