using Kuroe.Agent.Tools;
using Kuroe.Agent.Turns;
using Kuroe.Shared.Agent.Tools;
using Kuroe.Workflows.Tasks;

namespace Kuroe.Tools;

/// <summary>检查叶子交回结论的通道。载体按回合换一份，结论只会落到该回合所属的条目上。</summary>
public sealed class VerdictTool : IScopedAgentTool
{
    private readonly UnitSubmitter _intake;
    private readonly TurnScope? _scope;

    /// <summary>容器装配用的载体，尚未绑定回合。</summary>
    public VerdictTool(UnitSubmitter intake)
    {
        _intake = intake;
        Functions = Declare(intake, null);
    }

    private VerdictTool(UnitSubmitter intake, TurnScope scope)
    {
        _intake = intake;
        _scope = scope;
        Functions = Declare(intake, scope);
    }

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>换一份绑定到该回合的载体。</summary>
    public IAgentTool ForTurn(TurnScope scope) => new VerdictTool(_intake, scope);

    /// <summary>声明绑定到该回合上的提交函数。</summary>
    private static IReadOnlyList<ToolFunction> Declare(UnitSubmitter intake, TurnScope? scope) =>
    [
        new ToolFunction("SubmitVerdict", "交回检查结论。passed 为真表示通过；不通过时 findings 要逐条列出问题。",
            [
                new ToolParameter("passed", "检查是否通过", Flag: true, Required: true),
                new ToolParameter("findings", "不通过时逐条列出问题"),
            ],
            arguments => arguments.Flag("passed") is { } passed
                ? intake.SubmitVerdict(scope, passed, arguments.Text("findings") ?? string.Empty)
                : "被拒绝：passed 缺失或不是 true/false。"),
    ];
}
