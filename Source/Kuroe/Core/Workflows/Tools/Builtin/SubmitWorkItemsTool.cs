using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.Execution.Turns;
using Kuroe.Tool;
using Kuroe.Flow.Execution.Tasks;

namespace Kuroe.Workflows.Tools.Builtin;

/// <summary>声明 WorkItems 端口的执行节点交回条目拆分的通道。载体按回合换一份，提交时才知道是哪个 run 在交。</summary>
public sealed class SubmitWorkItemsTool : IScopedTool
{
    private readonly WorkItemSubmitter _intake;

    /// <summary>容器装配用的载体，尚未绑定回合。</summary>
    public SubmitWorkItemsTool(WorkItemSubmitter intake)
    {
        _intake = intake;
        Functions = Declare(intake, null);
    }

    private SubmitWorkItemsTool(WorkItemSubmitter intake, TurnScope scope)
    {
        _intake = intake;
        Functions = Declare(intake, scope);
    }

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>换一份绑定到该回合的载体。</summary>
    public ITool ForTurn(TurnScope scope) => new SubmitWorkItemsTool(_intake, scope);

    /// <summary>把契约定义绑上提交该回合条目拆分的调用体。</summary>
    private static IReadOnlyList<ToolFunction> Declare(WorkItemSubmitter intake, TurnScope? scope) =>
    [
        ContractToolDefinitions.WorkItems.Bind(arguments =>
            intake.SubmitItems(scope?.Run ?? default, arguments.Text(new ToolName("itemsJson")) ?? string.Empty)),
    ];
}
