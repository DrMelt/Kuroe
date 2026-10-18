using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.Execution.Turns;
using Kuroe.Tool;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Execution.Tasks;

namespace Kuroe.Workflows.Tools.Builtin;

/// <summary>执行节点声明本轮状态的通道：交付完毕或无法完成。载体按回合换一份，声明时才知道是哪个 run 在报。</summary>
public sealed class StatusTool : IScopedTool
{
    private readonly StatusSubmitter _intake;
    private readonly TaskRegistry _registry;

    /// <summary>容器装配用的载体，尚未绑定回合。</summary>
    public StatusTool(StatusSubmitter intake, TaskRegistry registry)
    {
        _intake = intake;
        _registry = registry;
        Functions = Declare(intake, null, null);
    }

    private StatusTool(StatusSubmitter intake, TaskRegistry registry, TurnScope scope)
    {
        _intake = intake;
        _registry = registry;
        Functions = Declare(intake, scope, PortsOf(scope));
    }

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>换一份绑定到该回合的载体。</summary>
    public ITool ForTurn(TurnScope scope) => new StatusTool(_intake, _registry, scope);

    /// <summary>绑定回合所属 run 声明的 Text 输出端口，没有声明时为空。</summary>
    private IReadOnlyList<Port>? PortsOf(TurnScope scope) =>
        _registry.FindRun(scope.Run) is { IsError: false } found && found.Value.Context.TextPorts.Count > 0
            ? found.Value.Context.TextPorts
            : null;

    /// <summary>把契约定义绑上声明该回合状态的调用体，有端口清单时写进说明。</summary>
    private static IReadOnlyList<ToolFunction> Declare(StatusSubmitter intake, TurnScope? scope, IReadOnlyList<Port>? ports) =>
    [
        ContractToolDefinitions.Status(ports).Bind(arguments => intake.SubmitVerdict(
            scope?.Run ?? default,
            arguments.Text(new ToolName("status")),
            arguments.Text(new ToolName("reason")))),
    ];
}
