using Kuroe.Flows.Execution.Runs;
using Kuroe.Flows.Shared.Execution;
using Kuroe.Flows.Shared.RuntimeGraph;
using Kuroe.Flows.Shared.StaticGraph;
using Kuroe.Flows.Execution.Tasks;
using Kuroe.Flows.RuntimeGraph;
using ExecutableNode = Kuroe.Flows.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flows.RuntimeNodes;

/// <summary>一个执行节点在任务内的运行时对象：执行节点定义与节点自身状态的合体，执行单元的产出与交回在 RuntimeInstance 上。
/// 节点状态只经推进方法改动，读写都要求持有任务 Gate。</summary>
internal sealed class RuntimeExecutable(ExecutableNode executable) : NodeRuntime(executable), IInputProgress
{
    /// <summary>提交时锁定的执行节点定义，不可变。</summary>
    public ExecutableNode Executable { get; } = executable;

    /// <summary>执行节点的展开模式：整节点一次执行还是按实例逐步启动。</summary>
    public NodeMode Mode => Executable.Execution.Mode;

    /// <summary>已确定的实例条目集，整节点与尚未展开时为空。实例集确定后不再变化，与条目执行单元一一对应。</summary>
    public override IReadOnlyList<int> Items => _items;

    /// <summary>实例条目集的顺序视图，仅在实例集确定时与条目执行单元一并写入。</summary>
    private List<int> _items = [];

    /// <summary>实例集已确定。</summary>
    public bool Expanded { get; private set; }

    /// <summary>本节点交回的条目拆分，尚未交回时为空。按条目展开的下游由它取实例空间与条目内容，判定按它读数。</summary>
    public WorkItemList? Split { get; private set; }

    /// <summary>记下本节点交回的条目拆分，同一节点重交覆盖。</summary>
    public void SubmitSplit(WorkItemList split) => Split = split;

    /// <summary>整节点执行单元，不展开的节点与整节点实例共用这一个，按需建出。</summary>
    private RuntimeInstance? _wholeInstance;

    /// <summary>条目执行单元，按条目序，键与 <see cref="Items"/> 一致，只在实例集确定时登记。</summary>
    private readonly Dictionary<int, RuntimeInstance> _itemInstances = [];

    /// <summary>某执行单元的运行时节点，尚未建出时为空。只读路径用它，不建实例。要求持有任务 Gate。</summary>
    public RuntimeInstance? FindRuntimeInstance(int? item) =>
        item is { } index ? _itemInstances.GetValueOrDefault(index) : _wholeInstance;

    /// <summary>某执行单元的运行时节点。条目执行单元必须已登记，条目序为空时给按需建出的整节点执行单元。要求持有任务 Gate。</summary>
    public RuntimeInstance Instance(int? item) =>
        item is { } index
            ? FindRuntimeInstance(index) ?? throw new InvalidOperationException($"节点「{Name}」没有条目 {index} 的执行单元。")
            : _wholeInstance ??= new RuntimeInstance(null, null);

    /// <summary>输入侧消费账，版本驱动的判定按它比较来源版本。</summary>
    private readonly ConsumptionLedger _consumed = new();

    /// <summary>在跑的实例数。推进时经 AddActive 与 ReleaseActive 改动。</summary>
    public int Active { get; private set; }

    private bool _canceled;
    private bool _rerunRequested;

    /// <summary>是否有执行单元停在等待返工或放行。</summary>
    public bool HasBlocked =>
        _wholeInstance is { Blocked: true } || _itemInstances.Values.Any(instance => instance.Blocked);

    /// <summary>停在阻塞的执行单元：整节点执行单元在前，条目执行单元按条目序。</summary>
    public IEnumerable<RuntimeInstance> BlockedInstances
    {
        get
        {
            if (_wholeInstance is { Blocked: true } whole)
            {
                yield return whole;
            }

            foreach (RuntimeInstance instance in _itemInstances.Values.Where(instance => instance.Blocked))
            {
                yield return instance;
            }
        }
    }

    /// <summary>某执行单元是否停在阻塞，没有该执行单元时为假。要求持有任务 Gate。</summary>
    public bool IsBlocked(int? item) => FindRuntimeInstance(item)?.Blocked == true;

    public override bool Canceled => _canceled;

    /// <summary>被作废的重跑目标：下一次启动判定越过就绪条件直接重启一次。</summary>
    public bool RerunRequested => _rerunRequested;

    /// <summary>记下重跑请求，供返工作废后的重启判定使用。</summary>
    public void RequestRerun() => _rerunRequested = true;

    /// <summary>重跑请求已消耗。</summary>
    public void ClearRerun() => _rerunRequested = false;

    /// <summary>产出已放行、可被下游消费：产出已发布且不在阻塞与取消。PerItem 要求全部实例已发布。</summary>
    public override bool Released =>
        (Mode == NodeMode.PerItem
            ? Expanded && Items.All(item => Complete(item))
            : Complete(null))
        && !HasBlocked && !Canceled;

    /// <summary>单调的发布计数，作废归零发表号但不清计数。消费账按它记账，重跑后版本不回退。</summary>
    public override long Revision => _stamp;
    private long _stamp;

    /// <summary>是否仍有在跑的实例。</summary>
    public bool HasActive => Active > 0;

    /// <summary>固定实例集并标为已展开，实例展开的唯一点：条目顺序视图与条目执行单元在此一并写入，条目内容取自实例空间的拆分。要求持有任务 Gate。</summary>
    public void AdoptItems(IReadOnlyList<int> items, WorkItemList? space)
    {
        _items = [.. items];
        Expanded = true;
        foreach (int item in items)
        {
            _itemInstances[item] = new RuntimeInstance(item, space?.Items.FirstOrDefault(entry => entry.Index == item));
        }
    }

    /// <summary>该实例是否已有发表产出。</summary>
    public override bool ProducedItem(int item) => Complete(item);

    /// <summary>启动即登记方案内全部取值来源的当前版本，防止同版本重复触发。</summary>
    public void Consume(NodeInputPlan plan, IExecutionSources sources) => _consumed.Record(plan, sources);

    IReadOnlyList<int>? IInputProgress.AdoptedItems => Expanded ? _items : null;

    long IInputProgress.ConsumedRevision(int source) => _consumed.Revision(source);

    bool IInputProgress.ConsumedOnce(int source) => _consumed.ConsumedOnce(source);

    bool IInputProgress.Produced(int? item) => Complete(item);

    int IInputProgress.Runs(int? item) => ExecutionCount(item);

    bool IInputProgress.ForceRerun => RerunRequested;

    /// <summary>执行单元是否已有发表产出。</summary>
    public bool Complete(int? item) => FindRuntimeInstance(item)?.PublishedRun is not null;

    /// <summary>产出发布：产出锚指向这次执行，发表号单调递增，下游据此重新评估。</summary>
    public void Publish(Run run)
    {
        Instance(run.Context.ItemIndex).Publish(run);
        _stamp++;
    }

    /// <summary>记一轮派发，次数按执行单元累计。调用点唯一：WorkTask.Attach。</summary>
    public void RecordRun(int? item) => Instance(item).RecordRun();

    /// <summary>某执行单元当前已启动的 run 数，没有该执行单元时为 0。</summary>
    public int ExecutionCount(int? item) => FindRuntimeInstance(item)?.Runs ?? 0;

    /// <summary>作废执行单元的已发表产出与交回，返工起点用它让下游重新等待。PerItem 执行节点不带条目时清空全部条目执行单元。
    /// 阻塞与返工目标不在此清除：达到执行上限的目标长期留驻，由 <see cref="FinishRework"/> 按处理进度解除。</summary>
    public void Invalidate(int? item)
    {
        if (item is { } index)
        {
            FindRuntimeInstance(index)?.Invalidate();
            return;
        }

        if (Mode == NodeMode.PerItem)
        {
            foreach (RuntimeInstance instance in _itemInstances.Values)
            {
                instance.Invalidate();
            }

            return;
        }

        FindRuntimeInstance(null)?.Invalidate();
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

    /// <summary>把一个执行单元置为阻塞并合并记下返工目标，重复目标不重复收集。</summary>
    public void EnterBlocked(int? item, IReadOnlyList<(int Node, int? Item)> targets) =>
        Instance(item).EnterBlocked(targets);

    /// <summary>移除一个执行单元本次返工处理的目标，全部处理后解除阻塞。</summary>
    public void FinishRework(int? item, IReadOnlyList<(int Node, int? Item)> handled) =>
        Instance(item).FinishRework(handled);

    /// <summary>随任务进入取消态。</summary>
    public override void Cancel() => _canceled = true;

    /// <summary>按输出端口取单段文本：Context 端口给拼合文本，WorkItems 端口不产生文本，Text 端口取该节点交回的端口值。
    /// PerItem 给全部已发布实例的该端口值按实例序拼接。</summary>
    public override string? PortText(INodeTask task, PortName port)
    {
        PortType? type = Executable.FindPort(port)?.Type;
        if (type == PortType.Context)
        {
            return ContextOutput();
        }

        if (type == PortType.WorkItems)
        {
            // 条目拆分是结构化产出，不产生可注入文本
            return null;
        }

        if (Mode == NodeMode.PerItem)
        {
            string[] parts = [.. Items
                .Where(item => Complete(item))
                .Select(item => PortValue(port, item))
                .OfType<string>()
                .Where(value => value.Length > 0)];

            return parts.Length == 0 ? null : string.Join("\n\n", parts);
        }

        return PortValue(port, null);
    }

    /// <summary>按端口取该执行单元已发布产出的一个返回值：命名端口取交回段。未发布或未交回时为空。</summary>
    private string? PortValue(PortName port, int? item)
    {
        RuntimeInstance? instance = FindRuntimeInstance(item);

        return instance?.PublishedRun is { } run ? instance.Ports(run.Id)?.GetValueOrDefault(port) : null;
    }

    /// <summary>按输出端口取可注入的产出消息集：PerItem 逐实例，其余单条，Context 端口给装配帧，Items 端口不注入。
    /// item 指定时只取该实例的产出，供同序号实例取值，不受整节点放行状态约束。</summary>
    public override IReadOnlyList<ContextMessage> OutputMessages(INodeTask task, PortName port, int? item)
    {
        PortType? type = Executable.FindPort(port)?.Type;
        if (type == PortType.Context)
        {
            // PerItem 来源的上下文端口逐实例展开各帧，与其它端口逐实例取产出一致
            if (item is null && Mode == NodeMode.PerItem)
            {
                List<ContextMessage> frames = [];
                foreach (int published in Items.Where(index => Complete(index)))
                {
                    frames.AddRange(ContextFrameMessages(published));
                }

                return frames;
            }

            return ContextFrameMessages(item);
        }

        if (type == PortType.WorkItems)
        {
            // 条目拆分是结构化产出，不注入文本上下文
            return [];
        }

        if (item is { } index)
        {
            return SingleMessage(port, index);
        }

        if (!Released)
        {
            return [];
        }

        if (Mode == NodeMode.PerItem)
        {
            List<ContextMessage> messages = [];
            foreach (int published in Items.Where(published => Complete(published)))
            {
                messages.AddRange(SingleMessage(port, published));
            }

            return messages;
        }

        return SingleMessage(port, null);
    }

    /// <summary>按端口取该实例或整节点的一条产出消息。取值或出处缺失时不产生消息。</summary>
    private IReadOnlyList<ContextMessage> SingleMessage(PortName port, int? item)
    {
        if (FindRuntimeInstance(item)?.PublishedRun is not { } run
            || PortValue(port, item) is not { Length: > 0 } content)
        {
            return [];
        }

        string prefix = item is { } index
            ? $"条目「{ItemTitle(index)}」在节点「{Name}」的端口「{port}」产出：\n"
            : $"节点「{Name}」的端口「{port}」产出：\n";

        return [new ContextMessage(MessageRole.User, $"{prefix}{content}", new RunSource(run.Id, Name))];
    }

    /// <summary>上下文输出端口的注入消息：来源 run 的装配帧逐条保留，指令单列。</summary>
    private List<ContextMessage> ContextFrameMessages(int? item)
    {
        if (FindRuntimeInstance(item)?.PublishedRun is not { } frameRun)
        {
            return [];
        }

        ContextFrame frame = new(frameRun.Id, Name, item, frameRun.Context.Seed, frameRun.Context.Instruction);
        List<ContextMessage> messages =
        [
            .. frame.Messages,
            new(MessageRole.User, $"节点「{frame.Node}」的指令：\n{frame.Instruction}", new ContextFrameSource(frame.Run, frame.Node, frame.Item)),
        ];

        return messages;
    }

    /// <summary>条目的标题，条目内容不在实例上时退回序号。条目身份只在归属空间内有效。</summary>
    private string ItemTitle(int itemIndex) => FindRuntimeInstance(itemIndex)?.Item?.Title ?? $"条目 {itemIndex + 1}";

    /// <summary>已发布产出那次执行的装配上下文拼合文本，指令单列。</summary>
    private string? ContextOutput()
    {
        if (FindRuntimeInstance(null)?.PublishedRun is not { } frameRun)
        {
            return null;
        }

        ContextFrame frame = new(frameRun.Id, Name, null, frameRun.Context.Seed, frameRun.Context.Instruction);
        string content = string.Join("\n\n", frame.Messages.Select(message => message.Text));

        return $"{content}\n\n指令：\n{frame.Instruction}";
    }

    /// <summary>执行节点在快照里的一刻状态。</summary>
    public override ExecutableStateSnapshot Snapshot()
    {
        List<int> items = Expanded ? [.. Items] : [];
        int completed;
        if (Expanded)
        {
            completed = items.Count(item => Complete(item));
        }
        else
        {
            completed = Complete(null) ? 1 : 0;
        }

        NodeState state;
        if (Canceled)
        {
            state = NodeState.Canceled;
        }
        else if (HasBlocked)
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
            state = Complete(null) ? NodeState.Done : NodeState.Pending;
        }

        List<int> blocked = [.. BlockedInstances.Select(instance => instance.ItemIndex).OfType<int>()];

        return new ExecutableStateSnapshot(Index, state, items, completed, blocked);
    }
}
