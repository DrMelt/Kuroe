using Kuroe.Agent.Tools;
using Kuroe.Agent.Turns;
using Kuroe.Shared.Agent.Tools;
using Kuroe.Workflows.Tasks;

namespace Kuroe.Tools;

/// <summary>规划步骤交回条目拆分的通道。载体按回合换一份，提交时才知道是哪个 agent 在交。</summary>
public sealed class PlanTool : IScopedAgentTool
{
    private readonly UnitSubmitter _intake;
    private readonly TurnScope? _scope;

    /// <summary>容器装配用的载体，尚未绑定回合。</summary>
    public PlanTool(UnitSubmitter intake)
    {
        _intake = intake;
        Functions = Declare(intake, null);
    }

    private PlanTool(UnitSubmitter intake, TurnScope scope)
    {
        _intake = intake;
        _scope = scope;
        Functions = Declare(intake, scope);
    }

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>换一份绑定到该回合的载体。</summary>
    public IAgentTool ForTurn(TurnScope scope) => new PlanTool(_intake, scope);

    /// <summary>声明绑定到该回合上的提交函数。</summary>
    private static IReadOnlyList<ToolFunction> Declare(UnitSubmitter intake, TurnScope? scope) =>
    [
        new ToolFunction("SubmitPlanItems",
            "提交本步骤的条目拆分。itemsJson 是对象数组的 JSON 文本，每项含 Title、Instruction、Acceptance。",
            [new ToolParameter("itemsJson", "对象数组的 JSON 文本，每项含 Title、Instruction、Acceptance", Required: true)],
            arguments => intake.SubmitPlan(scope, arguments.Text("itemsJson") ?? string.Empty)),
    ];
}
