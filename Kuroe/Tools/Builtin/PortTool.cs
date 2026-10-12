using Kuroe.Executions.Tools;
using Kuroe.Executions.Turns;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Workflows.Execution.Tasks;

namespace Kuroe.Tools.Builtin;

/// <summary>声明输出端口的执行节点交回命名端口产出的通道。载体按回合换一份，提交时才知道是哪个 run 在交。</summary>
public sealed class PortTool : IScopedTool
{
    private readonly PortSubmitter _intake;
    private readonly TaskRegistry _registry;

    /// <summary>容器装配用的载体，尚未绑定回合。</summary>
    public PortTool(PortSubmitter intake, TaskRegistry registry)
    {
        _intake = intake;
        _registry = registry;
        Functions = Declare(intake, null, null);
    }

    private PortTool(PortSubmitter intake, TaskRegistry registry, TurnScope scope)
    {
        _intake = intake;
        _registry = registry;
        Functions = Declare(intake, scope, PortsOf(scope));
    }

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>换一份绑定到该回合的载体。</summary>
    public ITool ForTurn(TurnScope scope) => new PortTool(_intake, _registry, scope);

    /// <summary>绑定回合所属 run 声明的输出端口，没有声明时为空。</summary>
    private IReadOnlyList<Port>? PortsOf(TurnScope scope) =>
        _registry.FindRun(scope.Run) is not { IsError: false } found
        || found.Value.Context.OutputPorts is not { Count: > 0 } ports
            ? null
            : ports;

    /// <summary>把契约定义绑上提交该回合端口内容的调用体，有端口清单时写进说明。</summary>
    private static IReadOnlyList<ToolFunction> Declare(PortSubmitter intake, TurnScope? scope, IReadOnlyList<Port>? ports) =>
    [
        ContractTools.WithPorts(ContractTools.Port, ports).Bind(arguments =>
            intake.SubmitValues(scope, arguments.Text(new ToolName("valuesJson")) ?? string.Empty)),
    ];
}
