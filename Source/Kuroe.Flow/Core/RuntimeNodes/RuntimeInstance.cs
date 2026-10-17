using Kuroe.Flow.Execution.Runs;
using Kuroe.Flow.RuntimeGraph;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.RuntimeNodes;

/// <summary>一个执行单元的运行时节点：命名端口值与本轮结论按 run 记，发布后只保留已发布那次，产出锚指向已发布的那次执行，
/// 条目内容在实例空间确定时随实例落地。条目执行单元与整节点执行单元共用这一形状，读写都要求持有任务 Gate。</summary>
internal sealed class RuntimeInstance(int? itemIndex, WorkItem? item)
{
    private readonly Dictionary<RunId, Submission> _submissions = [];

    /// <summary>条目序号，整节点执行单元为空。</summary>
    public int? ItemIndex { get; } = itemIndex;

    /// <summary>条目内容，整节点执行单元与实例空间外的条目为空。</summary>
    public WorkItem? Item { get; } = item;

    /// <summary>已派发的 run 数，本执行单元的执行次数。</summary>
    public int Runs { get; private set; }

    /// <summary>已发布产出的那次执行，未发布时为空。</summary>
    public Run? PublishedRun { get; private set; }

    /// <summary>某次执行交回的命名端口值，未交回时为空。</summary>
    public IReadOnlyDictionary<PortName, string>? Ports(RunId run) =>
        _submissions.GetValueOrDefault(run)?.Ports;

    /// <summary>某次执行声明的本轮结论，未声明时为空。</summary>
    public RunVerdict? Verdict(RunId run) =>
        _submissions.GetValueOrDefault(run)?.Verdict;

    /// <summary>记下一次执行交回的命名端口值。</summary>
    public void CollectPorts(RunId run, IReadOnlyDictionary<PortName, string> values) =>
        _submissions[run] = (_submissions.GetValueOrDefault(run) ?? new Submission()) with { Ports = values };

    /// <summary>记下一次执行声明的本轮结论，同一执行重复声明覆盖。</summary>
    public void CollectVerdict(RunId run, RunVerdict verdict) =>
        _submissions[run] = (_submissions.GetValueOrDefault(run) ?? new Submission()) with { Verdict = verdict };

    /// <summary>记一轮派发。</summary>
    public void RecordRun() => Runs++;

    /// <summary>是否停在阻塞等待返工。</summary>
    public bool Blocked { get; private set; }

    private readonly List<(NodePath Node, int? Item)> _reworkTargets = [];

    /// <summary>停在本执行单元时记下的返工目标，全部处理后解除阻塞。</summary>
    public IReadOnlyList<(NodePath Node, int? Item)> ReworkTargets => _reworkTargets;

    /// <summary>置为阻塞并合并记下返工目标，重复目标不重复收集。</summary>
    public void EnterBlocked(IReadOnlyList<(NodePath Node, int? Item)> targets)
    {
        Blocked = true;
        foreach ((NodePath node, int? entry) in targets)
        {
            if (!_reworkTargets.Contains((node, entry)))
            {
                _reworkTargets.Add((node, entry));
            }
        }
    }

    /// <summary>移除本次返工处理的目标，全部处理后解除阻塞。</summary>
    public void FinishRework(IReadOnlyList<(NodePath Node, int? Item)> handled)
    {
        foreach ((NodePath node, int? entry) in handled)
        {
            _reworkTargets.Remove((node, entry));
        }

        if (_reworkTargets.Count == 0)
        {
            Blocked = false;
        }
    }

    /// <summary>产出发布：产出锚指向这次执行，只保留这次执行的交回记录，历史执行的交回不再被读取。</summary>
    public void Publish(Run run)
    {
        PublishedRun = run;
        Submission? kept = _submissions.GetValueOrDefault(run.Id);
        _submissions.Clear();
        if (kept is not null)
        {
            _submissions[run.Id] = kept;
        }
    }

    /// <summary>作废产出：移除产出锚并清掉交回记录，产出随下一次执行重新给出。</summary>
    public void Invalidate()
    {
        PublishedRun = null;
        _submissions.Clear();
    }

    /// <summary>一次执行交回的内容。</summary>
    private sealed record Submission(
        IReadOnlyDictionary<PortName, string>? Ports = null,
        RunVerdict? Verdict = null);
}
