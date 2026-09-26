using Kuroe.Agent.Runs;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.Tasks;

/// <summary>任务内的一个执行单元：不展开的叶子只有一个，按条目展开时每个条目一个。
/// 单元沿流程叶子推进，每个叶子上由一个 agent 承担。状态只经本类的方法改动，
/// 调用方须持有所属任务的 Gate。</summary>
public sealed class WorkUnit(int? itemIndex, PlanItem? item, int nodeCursor)
{
    private readonly Dictionary<int, AgentRun> _byNode = [];

    /// <summary>所属条目序号，整叶单元为空。</summary>
    public int? ItemIndex { get; } = itemIndex;

    /// <summary>规划交回的条目，整叶单元为空。</summary>
    public PlanItem? Item { get; } = item;

    /// <summary>下一个待执行的叶子序号，等于叶子数时该单元走完。</summary>
    public int NodeCursor { get; private set; } = nodeCursor;

    /// <summary>单元是否还在自动推进。</summary>
    public UnitState State { get; private set; } = UnitState.Working;

    /// <summary>派出的 agent 已跑完但推进器尚未处理，处理前该单元不再派发。</summary>
    public bool Pending { get; private set; }

    /// <summary>检查结论。未检查时为空，返工期间保留历史值供下一轮实施阅读。</summary>
    internal CheckResult? LastCheck { get; private set; }

    /// <summary>各叶子上该单元最近一次的 agent，下游叶子据此引用上游产出。</summary>
    internal IReadOnlyDictionary<int, AgentRun> Nodes => _byNode;

    /// <summary>最近一次检查结论是否通过。</summary>
    public UnitVerdict Verdict => LastCheck is null ? UnitVerdict.NotChecked
        : LastCheck.Passed ? UnitVerdict.Verified : UnitVerdict.Rejected;

    /// <summary>最近一次检查的整改意见。</summary>
    public string? Findings => LastCheck?.Findings;

    /// <summary>检查轮次：最近一次检查的轮次，未检查时视为第一轮。</summary>
    public int Attempts => LastCheck?.Round ?? 1;

    /// <summary>把 agent 挂到本单元所属的叶子上，处理它的产出前不再派发。</summary>
    internal void Attach(AgentRun run)
    {
        _byNode[run.Context.NodeIndex] = run;
        Pending = true;
    }

    /// <summary>按条目展开出的单元继承前置叶子已交回的产出。</summary>
    internal void Inherit(WorkUnit upstream)
    {
        foreach ((int index, AgentRun run) in upstream._byNode)
        {
            _byNode[index] = run;
        }
    }

    /// <summary>该单元的 agent 已交回产出，可以处理了。</summary>
    internal void Release() => Pending = false;

    /// <summary>检查结论落地为最近一次结论。同一次检查只有首个交点，返回是否记录成功。</summary>
    internal bool RecordCheck(AgentRun run, bool passed, string findings)
    {
        if (LastCheck?.Origin == run.Id)
        {
            return false;
        }

        LastCheck = new CheckResult(run.Context.Attempt, passed, findings, run.Id, run.Context.NodeName);

        return true;
    }

    /// <summary>本叶产出已就绪，开下一叶。</summary>
    internal void AdvanceTo(int nodeIndex) => (NodeCursor, State) = (nodeIndex, UnitState.Working);

    /// <summary>全部叶子走完。</summary>
    internal void Finish(int nodeCursor) => (NodeCursor, State) = (nodeCursor, UnitState.Done);

    /// <summary>停在当前叶，等人批准才开下一叶。</summary>
    internal void AwaitApproval() => State = UnitState.AwaitingApproval;

    /// <summary>停在这里，需要人返工或放行才会继续。</summary>
    internal void Block() => State = UnitState.Blocked;

    /// <summary>随任务取消。</summary>
    internal void Cancel() => State = UnitState.Canceled;

    /// <summary>退回某个实施叶再来一轮。检查结论保留，供下一轮实施读轮次与意见。</summary>
    internal void Rework(int? nodeIndex)
    {
        if (nodeIndex is { } from)
        {
            NodeCursor = from;
        }

        State = UnitState.Working;
        Pending = false;
    }
}
