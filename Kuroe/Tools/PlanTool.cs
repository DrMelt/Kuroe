using System.ComponentModel;
using Kuroe.Agent;
using Kuroe.Workflows;

namespace Kuroe.Tools;

/// <summary>规划步骤交回条目拆分的通道。载体按回合换一份，提交时才知道是哪个 agent 在交。</summary>
public sealed class PlanTool : IScopedAgentTool
{
    private readonly UnitSubmitter _intake;
    private readonly TurnScope? _scope;

    public PlanTool(UnitSubmitter intake) => _intake = intake;

    private PlanTool(UnitSubmitter intake, TurnScope scope) => (_intake, _scope) = (intake, scope);

    public IAgentTool ForTurn(TurnScope scope) => new PlanTool(_intake, scope);

    [Description("提交本步骤的条目拆分。itemsJson 是对象数组的 JSON 文本，每项含 Title、Instruction、Acceptance。")]
    public string SubmitPlanItems(string itemsJson) => _intake.SubmitPlan(_scope, itemsJson);
}
