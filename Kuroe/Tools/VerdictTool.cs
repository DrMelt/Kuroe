using System.ComponentModel;
using Kuroe.Agent;
using Kuroe.Workflows;

namespace Kuroe.Tools;

/// <summary>检查步骤交回结论的通道。载体按回合换一份，结论只会落到该回合所属的条目上。</summary>
public sealed class VerdictTool : IScopedAgentTool
{
    private readonly UnitSubmitter _intake;
    private readonly TurnScope? _scope;

    public VerdictTool(UnitSubmitter intake) => _intake = intake;

    private VerdictTool(UnitSubmitter intake, TurnScope scope) => (_intake, _scope) = (intake, scope);

    public IAgentTool ForTurn(TurnScope scope) => new VerdictTool(_intake, scope);

    [Description("交回检查结论。passed 为真表示通过；不通过时 findings 要逐条列出问题。")]
    public string SubmitVerdict(bool passed, string findings) => _intake.SubmitVerdict(_scope, passed, findings);
}
