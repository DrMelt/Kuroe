using Kuroe.Agent.Runs;

namespace Kuroe.Workflows.Tasks;

/// <summary>任务内的一个执行单元：不展开的步骤只有一个，按条目展开时每个条目一个。
/// 单元沿流程步骤顺序推进，每个步骤上由一个 agent 承担。状态只经本类的方法改动，
/// 调用方须持有所属任务的 Gate。</summary>
public sealed class WorkUnit(int? itemIndex, PlanItem? item, int stepCursor)
{
    private readonly Dictionary<int, AgentRun> _byStep = [];

    public int? ItemIndex { get; } = itemIndex;

    public PlanItem? Item { get; } = item;

    /// <summary>下一个待执行的步骤序号，等于步骤数时该单元走完。</summary>
    public int StepCursor { get; private set; } = stepCursor;

    public UnitState State { get; private set; } = UnitState.Working;

    public UnitVerdict Verdict { get; private set; } = UnitVerdict.NotChecked;

    /// <summary>检查不通过时的问题清单，返工时写进上下文。</summary>
    public string? Findings { get; private set; }

    /// <summary>已实施的轮数，返工时递增。</summary>
    public int Attempts { get; private set; } = 1;

    /// <summary>派出的 agent 已跑完但推进器尚未处理，处理前该单元不再派发。</summary>
    public bool Pending { get; private set; }

    /// <summary>各步骤上该单元最近一次的 agent，下游步骤据此引用上游产出。</summary>
    internal IReadOnlyDictionary<int, AgentRun> Steps => _byStep;

    /// <summary>把 agent 挂到本单元所属的步骤上，处理它的产出前不再派发。</summary>
    internal void Attach(AgentRun run)
    {
        _byStep[run.Context.StepIndex] = run;
        Pending = true;
    }

    /// <summary>按条目展开出的单元继承前置步骤已交回的产出。</summary>
    internal void Inherit(WorkUnit upstream)
    {
        foreach ((int stepIndex, AgentRun run) in upstream._byStep)
        {
            _byStep[stepIndex] = run;
        }
    }

    /// <summary>该单元的 agent 已交回产出，可以处理了。</summary>
    internal void Release() => Pending = false;

    /// <summary>检查步骤交回结论。</summary>
    internal void RecordVerdict(bool passed, string findings)
    {
        Verdict = passed ? UnitVerdict.Verified : UnitVerdict.Rejected;
        Findings = passed ? null : findings.Trim();
    }

    /// <summary>本步产出已就绪，开下一步。</summary>
    internal void AdvanceTo(int stepIndex) => (StepCursor, State) = (stepIndex, UnitState.Working);

    /// <summary>全部步骤走完。</summary>
    internal void Finish(int stepCursor) => (StepCursor, State) = (stepCursor, UnitState.Done);

    /// <summary>停在当前步，等人批准才开下一步。</summary>
    internal void AwaitApproval() => State = UnitState.AwaitingApproval;

    /// <summary>停在这里，需要人返工或放行才会继续。</summary>
    internal void Block() => State = UnitState.Blocked;

    /// <summary>随任务取消。</summary>
    internal void Cancel() => State = UnitState.Canceled;

    /// <summary>退回某个实施步骤再来一轮；没有可退回的步骤时留在原地重试。</summary>
    internal void Rework(int? stepIndex)
    {
        if (stepIndex is { } from)
        {
            StepCursor = from;
        }

        Attempts++;
        Verdict = UnitVerdict.NotChecked;
        State = UnitState.Working;
        Pending = false;
    }
}
