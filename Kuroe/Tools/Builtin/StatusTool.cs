using Kuroe.Executions.Tools;
using Kuroe.Executions.Turns;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Workflows.Execution.Tasks;

namespace Kuroe.Tools.Builtin;

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

    /// <summary>绑定回合所属 run 声明的输出端口，没有声明时为空。</summary>
    private IReadOnlyList<Port>? PortsOf(TurnScope scope) =>
        _registry.FindRun(scope.Run) is not { IsError: false } found
        || found.Value.Context.OutputPorts is not { Count: > 0 } ports
            ? null
            : ports;

    /// <summary>声明绑定到该回合上的提交函数，描述里给出状态取值与仍需交回的端口。</summary>
    private static IReadOnlyList<ToolFunction> Declare(StatusSubmitter intake, TurnScope? scope, IReadOnlyList<Port>? ports)
    {
        string listed = ports is null ? string.Empty : $"本节点需要交回的端口：{ContractText.List(ports)}。";

        return
        [
            new ToolFunction(ToolName.ContractStatus,
                $"声明本执行节点这一轮的状态。status 取 completed 交付完毕或 blocked 无法完成，blocked 时必须给出 reason。{listed}",
                [
                    new ToolParameter(new ToolName("status"), "completed 或 blocked", Required: true),
                    new ToolParameter(new ToolName("reason"), "无法完成的原因，status 为 blocked 时必填"),
                ],
                arguments => intake.SubmitVerdict(
                    scope,
                    arguments.Text(new ToolName("status")),
                    arguments.Text(new ToolName("reason"))),
                ToolPath.ContractStatus),
        ];
    }
}
