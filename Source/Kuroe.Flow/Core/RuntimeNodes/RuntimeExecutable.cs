using Kuroe.Flow.Runtime;
using Kuroe.Flow.RuntimeGraph;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flow.RuntimeNodes;

/// <summary>一个执行节点在任务内的运行时对象：执行节点定义与节点自身状态的合体，交回与产出也记在它上面。
/// 待收口的交回按执行记，发布后只保留已发布那次，产出锚指向已发布的那次执行。
/// 节点状态只经推进方法改动，读写都要求持有任务 Gate。</summary>
internal sealed class RuntimeExecutable(ExecutableNode executable) : NodeRuntime(executable), IInputProgress
{
    /// <summary>提交时锁定的执行节点定义，不可变。</summary>
    public ExecutableNode Executable { get; } = executable;

    /// <summary>各次执行交回的内容，发布后只保留已发布那次。</summary>
    private readonly Dictionary<ExecutionId, Submission> _submissions = [];

    /// <summary>输入侧消费账，版本驱动的判定按它比较来源版本。</summary>
    private readonly ConsumptionLedger _consumed = new();

    /// <summary>停在本节点时记下的返工目标，全部处理后解除阻塞。</summary>
    private readonly List<NodePath> _reworkTargets = [];

    private bool _canceled;
    private bool _rerunRequested;
    private long _stamp;

    /// <summary>在跑的执行数。推进时经 AddActive 与 ReleaseActive 改动。</summary>
    public int Active { get; private set; }

    /// <summary>是否仍有在跑的执行。</summary>
    public bool HasActive => Active > 0;

    /// <summary>已发布产出的那次执行，未发布时为空。</summary>
    public Execution? PublishedExecution { get; private set; }

    /// <summary>已启动的执行数。</summary>
    public int Executions { get; private set; }

    /// <summary>是否停在阻塞等待返工。</summary>
    public bool Blocked { get; private set; }

    /// <summary>停在本节点时记下的返工目标，全部处理后解除阻塞。</summary>
    public IReadOnlyList<NodePath> ReworkTargets => _reworkTargets;

    /// <summary>被作废的重跑目标：下一次启动判定越过就绪条件直接重启一次。</summary>
    public bool RerunRequested => _rerunRequested;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    /// <summary>产出已放行、可被下游消费：产出已发布且不在阻塞与取消。</summary>
    public override bool Released => PublishedExecution is not null && !Blocked && !Canceled;

    /// <summary>单调的发布计数，作废归零产出锚但不清计数。消费账按它记账，重跑后版本不回退。</summary>
    public override long Revision => _stamp;

    /// <summary>随任务进入取消态。</summary>
    public override void Cancel() => _canceled = true;

    /// <summary>记下重跑请求，供返工作废后的重启判定使用。</summary>
    public void RequestRerun() => _rerunRequested = true;

    /// <summary>重跑请求已消耗。</summary>
    public void ClearRerun() => _rerunRequested = false;

    /// <summary>某次执行交回的命名端口值，未交回时为空。</summary>
    public IReadOnlyDictionary<PortName, string>? Ports(ExecutionId execution) =>
        _submissions.GetValueOrDefault(execution)?.Ports;

    /// <summary>某次执行上报的无法完成原因，未上报时为空。</summary>
    public string? CannotCompleteReason(ExecutionId execution) =>
        _submissions.GetValueOrDefault(execution)?.CannotCompleteReason;

    /// <summary>记下一次执行交回的命名端口值。</summary>
    public void CollectPorts(ExecutionId execution, IReadOnlyDictionary<PortName, string> values) =>
        _submissions[execution] = (_submissions.GetValueOrDefault(execution) ?? new Submission()) with { Ports = values };

    /// <summary>记下一次执行上报的无法完成原因，同一执行重复上报覆盖。</summary>
    public void CollectCannotComplete(ExecutionId execution, string reason) =>
        _submissions[execution] = (_submissions.GetValueOrDefault(execution) ?? new Submission()) with { CannotCompleteReason = reason };

    /// <summary>置为阻塞并合并记下返工目标，重复目标不重复收集。</summary>
    public void EnterBlocked(IReadOnlyList<NodePath> targets)
    {
        Blocked = true;
        foreach (NodePath target in targets.Where(target => !_reworkTargets.Contains(target)))
        {
            _reworkTargets.Add(target);
        }
    }

    /// <summary>移除本次返工处理的目标，全部处理后解除阻塞。</summary>
    public void FinishRework(IReadOnlyList<NodePath> handled)
    {
        foreach (NodePath target in handled)
        {
            _reworkTargets.Remove(target);
        }

        if (_reworkTargets.Count == 0)
        {
            Blocked = false;
        }
    }

    /// <summary>产出发布：产出锚指向这次执行，只保留这次执行的交回记录，发表号单调递增，下游据此重新评估。</summary>
    public void Publish(Execution execution)
    {
        PublishedExecution = execution;
        Submission? kept = _submissions.GetValueOrDefault(execution.Id);
        _submissions.Clear();
        if (kept is not null)
        {
            _submissions[execution.Id] = kept;
        }

        _stamp++;
    }

    /// <summary>作废已发布产出与交回记录，返工起点用它让下游重新等待。阻塞与返工目标不在此清除：达到执行上限的目标长期留驻，由 <see cref="FinishRework"/> 按处理进度解除。</summary>
    public void Invalidate()
    {
        PublishedExecution = null;
        _submissions.Clear();
    }

    /// <summary>记一轮派发。调用点唯一：WorkTask.Attach。</summary>
    public void RecordExecution() => Executions++;

    /// <summary>启动即登记方案内全部取值来源的当前版本，防止同版本重复触发。</summary>
    public void Consume(NodeInputPlan plan, IReadinessSources sources) => _consumed.Record(plan, sources);

    long IInputProgress.ConsumedRevision(NodePath source) => _consumed.Revision(source);

    int IInputProgress.Executions => Executions;

    bool IInputProgress.ForceRerun => RerunRequested;

    /// <summary>一个已启动的执行终结，在跑数减一。</summary>
    public void ReleaseActive()
    {
        if (Active > 0)
        {
            Active--;
        }
    }

    /// <summary>记下新启动的执行。</summary>
    public void AddActive() => Active++;

    /// <summary>按输出端口取单段文本：Context 端口给来源执行输入侧内容的拼合，Text 端口取已发布那次交回的端口值，其余端口不承载内容。</summary>
    public override string? PortText(INodeTask task, PortName port) =>
        Executable.FindPort(port)?.Type == PortType.Context ? Join(ContextInputs()) : PortValue(port);

    /// <summary>按输出端口取产出：Context 端口把来源执行输入侧取到的内容按原出处给出，Text 端口给已发布那次交回的端口值，其余端口不承载内容。</summary>
    public override IReadOnlyList<ContextValue> OutputValues(INodeTask task, PortName port)
    {
        if (Executable.FindPort(port)?.Type == PortType.Context)
        {
            return ContextInputs();
        }

        return PortValue(port) is { Length: > 0 } value && PublishedExecution is { } execution
            ? [new ContextValue(new ExecutionSource(execution.Id, Path, port), value)]
            : [];
    }

    /// <summary>来源执行输入侧取到的内容，按原出处给出。尚未产出时为空。</summary>
    private IReadOnlyList<ContextValue> ContextInputs() => PublishedExecution?.Input.Inputs ?? [];

    /// <summary>取值拼合为单段文本，没有内容时为空。</summary>
    private static string? Join(IReadOnlyList<ContextValue> values)
    {
        string[] parts = [.. values.Select(value => value.Text).Where(text => text.Length > 0)];

        return parts.Length == 0 ? null : string.Join("\n\n", parts);
    }

    /// <summary>按端口取已发布产出的一个返回值：只有 Text 端口承载内容，Signal 端口交回端口名但不作产出；未发布或未交回时为空。</summary>
    private string? PortValue(PortName port) =>
        Executable.FindPort(port) is { Type: PortType.Text } && PublishedExecution is { } execution
            ? Ports(execution.Id)?.GetValueOrDefault(port)
            : null;

    /// <summary>执行节点在快照里的一刻状态。</summary>
    public override ExecutableStateSnapshot Snapshot()
    {
        NodeState state;
        if (Canceled)
        {
            state = NodeState.Canceled;
        }
        else if (Blocked)
        {
            state = NodeState.Blocked;
        }
        else if (Active > 0)
        {
            state = NodeState.Running;
        }
        else
        {
            state = PublishedExecution is not null ? NodeState.Done : NodeState.Pending;
        }

        return new ExecutableStateSnapshot(Path, state);
    }

    /// <summary>一次执行交回的内容。</summary>
    private sealed record Submission(
        IReadOnlyDictionary<PortName, string>? Ports = null,
        string? CannotCompleteReason = null);
}