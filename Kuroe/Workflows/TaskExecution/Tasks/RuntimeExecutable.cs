using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;

namespace Kuroe.Workflows.TaskExecution.Tasks;

/// <summary>一个执行节点在任务内的运行时对象：执行节点定义与节点自身状态的合体。
/// 节点状态只经推进方法改动，读写都要求持有任务 Gate。</summary>
internal sealed class RuntimeExecutable(ExecutableNode executable) : RuntimeNode(executable)
{
    /// <summary>提交时锁定的执行节点定义，不可变。</summary>
    public ExecutableNode Executable { get; } = executable;

    /// <summary>输入侧消费账本，有入边的整节点执行节点装配，其余节点为空走原有判定。</summary>
    public NodeInput? Input { get; private set; }

    /// <summary>绑定输入消费账本。</summary>
    public void Bind(NodeInput input) => Input = input;

    /// <summary>执行节点的产出契约，决定收口方式与契约工具。</summary>
    public NodeOutput Output => Executable.Execution.Output;

    /// <summary>执行节点的展开模式：整节点一次执行还是按实例逐步启动。</summary>
    public NodeMode Mode => Executable.Execution.Mode;

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

    private readonly HashSet<RunId> _awaitingRuns = [];
    private bool _blocked;
    private bool _canceled;
    private bool _rerunRequested;
    private bool _awaitingInput;
    private string? _inputAnswer;

    /// <summary>有待批准的产出 run。</summary>
    public override bool Awaiting => _awaitingRuns.Count > 0;

    /// <summary>产出契约是否为用户输入：不启动 run，回答即产出。</summary>
    public bool IsInputOutput => Executable.Execution.Output == NodeOutput.Input;

    /// <summary>输入节点停在等待用户回答。</summary>
    public bool AwaitingInput => _awaitingInput;

    /// <summary>输入节点已收到的回答，未回答时为空。</summary>
    public string? InputAnswer => _inputAnswer;

    /// <summary>输入节点对用户的提示文本，未写时为通用提示。</summary>
    public string? Question => Executable.Execution.Question;

    /// <summary>等待批准的产出 run。</summary>
    public IReadOnlyCollection<RunId> AwaitingRuns => _awaitingRuns;

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

    /// <summary>清掉一个产出 run 的等待，返回该 run 原本在等待中。</summary>
    public bool ClearAwaiting(RunId run) => _awaitingRuns.Remove(run);

    /// <summary>清掉全部等待批准。</summary>
    public void ClearAllAwaiting() => _awaitingRuns.Clear();

    /// <summary>产出后按门控停在等待批准，Auto 不停。返回是否停留。</summary>
    public bool Park(RunId run)
    {
        if (Gate != NodeGate.Review)
        {
            return false;
        }

        _awaitingRuns.Add(run);

        return true;
    }

    /// <summary>输入节点停驻等待用户回答，重复停驻不改变状态。</summary>
    public void ParkInput()
    {
        if (!IsInputOutput)
        {
            return;
        }

        _awaitingInput = true;
    }

    /// <summary>记下回答并发布产出。输入节点复位后可再次回答。</summary>
    public void Answer(string text)
    {
        _awaitingInput = false;
        _inputAnswer = text;
        Publish(null);
    }

    /// <summary>输入节点复位：清掉发表位与当前回答，回到可挂起状态。</summary>
    public void ResetInput()
    {
        if (!IsInputOutput || _inputAnswer is null)
        {
            return;
        }

        _inputAnswer = null;
        Published = false;
        _awaitingInput = false;
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

    /// <summary>按输出端口取单段文本：命名端口取该节点交回的端口值，ContextOutput 给拼合文本。
    /// 输入节点的产出是当前回答，PerItem 给全部已发布实例的该端口值按实例序拼接。</summary>
    public override string? PortText(WorkTask task, PortName port)
    {
        if (port == PortNames.ContextOutput)
        {
            return ContextOutput(task);
        }

        if (IsInputOutput)
        {
            if (port != (Executable.Outputs.Count == 1 ? Executable.Outputs[0] : null))
            {
                return null;
            }

            return InputAnswer is { Length: > 0 } answer ? answer : null;
        }

        if (port == PortNames.Split)
        {
            // 拆分是结构化产出，不产生可注入文本
            return null;
        }

        if (Mode == NodeMode.PerItem)
        {
            string[] parts = [.. Items
                .Where(item => _publishedItems.Contains(item))
                .Select(item => PortValue(task, port, item))
                .OfType<string>()
                .Where(value => value.Length > 0)];

            return parts.Length == 0 ? null : string.Join("\n\n", parts);
        }

        return PortValue(task, port, null);
    }

    /// <summary>按端口取该实例或整节点的一个返回值：命名端口取交回段，输入节点取回答。</summary>
    private string? PortValue(WorkTask task, PortName port, int? item) =>
        task.PortValuesFor(Index, item)?.GetValueOrDefault(port);

    /// <summary>按输出端口取可注入的产出消息集：PerItem 逐实例，其余单条，上下文给装配帧。
    /// item 指定时只取该实例的产出，供对齐边按条目消费，不受整节点放行状态约束。</summary>
    public override IReadOnlyList<ContextMessage> OutputMessages(WorkTask task, PortName port, int? item)
    {
        if (port == PortNames.ContextOutput)
        {
            // PerItem 来源的上下文端口逐实例展开各帧，与其它端口逐实例取产出一致
            if (item is null && Mode == NodeMode.PerItem)
            {
                List<ContextMessage> frames = [];
                foreach (int published in Items.Where(index => Complete(index)))
                {
                    frames.AddRange(ContextFrameMessages(task, published));
                }

                return frames;
            }

            return ContextFrameMessages(task, item);
        }

        if (port == PortNames.Split)
        {
            // 拆分支票是结构化产出，不注入文本上下文
            return [];
        }

        if (item is { } index)
        {
            return SingleMessage(task, port, index);
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
                messages.AddRange(SingleMessage(task, port, published));
            }

            return messages;
        }

        return SingleMessage(task, port, null);
    }

    /// <summary>按端口取该实例或整节点的一条产出消息。输入节点给回答消息，出处指向用户输入。取值或出处缺失时不产生消息。</summary>
    private IReadOnlyList<ContextMessage> SingleMessage(WorkTask task, PortName port, int? item)
    {
        if (IsInputOutput)
        {
            if (port != (Executable.Outputs.Count == 1 ? Executable.Outputs[0] : null)
                || InputAnswer is not { Length: > 0 } answer)
            {
                return [];
            }

            return [new ContextMessage(MessageRole.User, $"节点「{Name}」的输入：\n{answer}", new InputSource(Name))];
        }

        if (LatestSucceededRun(task, Index, item) is not { } run
            || PortValue(task, port, item) is not { Length: > 0 } content)
        {
            return [];
        }

        string prefix = item is { } index
            ? $"条目「{ItemTitle(task, index)}」在节点「{Name}」的端口「{port}」产出：\n"
            : $"节点「{Name}」的端口「{port}」产出：\n";

        return [new ContextMessage(MessageRole.User, $"{prefix}{content}", new RunSource(run.Id, Name))];
    }

    /// <summary>上下文输出端口的注入消息：来源 run 的装配帧逐条保留，指令单列。</summary>
    private List<ContextMessage> ContextFrameMessages(WorkTask task, int? item)
    {
        if (LatestSucceededRun(task, Index, item) is not { } frameRun)
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

    /// <summary>条目的标题，取不到时退回序号。条目身份只在归属空间内有效。</summary>
    private string ItemTitle(WorkTask task, int itemIndex) =>
        task.Graph.ItemSpace(Index) is { } space && task.SplitFor(space) is { } split
            ? split.Items.FirstOrDefault(item => item.Index == itemIndex)?.Title ?? $"条目 {itemIndex + 1}"
            : $"条目 {itemIndex + 1}";

    /// <summary>最近成功收口 run 的装配上下文拼合文本，指令单列。</summary>
    private string? ContextOutput(WorkTask task)
    {
        if (LatestSucceededRun(task, Index, null) is not { } frameRun)
        {
            return null;
        }

        ContextFrame frame = new(frameRun.Id, Name, null, frameRun.Context.Seed, frameRun.Context.Instruction);
        string content = string.Join("\n\n", frame.Messages.Select(message => message.Text));

        return $"{content}\n\n指令：\n{frame.Instruction}";
    }

    /// <summary>本节点某实例最近一次成功收口且产出非空的 run。</summary>
    private static Run? LatestSucceededRun(WorkTask task, int node, int? item) =>
        task.Runs.LastOrDefault(run =>
            run.Context.NodeIndex == node
            && run.Context.ItemIndex == item
            && run.State == RunState.Succeeded
            && run.Result is { Length: > 0 });

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
        else if (IsInputOutput)
        {
            if (AwaitingInput)
            {
                state = NodeState.AwaitingInput;
            }
            else
            {
                state = Published ? NodeState.Done : NodeState.Pending;
            }
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

        return new ExecutableStateSnapshot(Index, state, items, completed, [.. _awaitingRuns.OrderBy(run => run.Value)], _inputAnswer);
    }
}
