using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.Execution.Turns;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Execution.Tasks;

namespace Kuroe.Tool.Contract;

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
        Functions = Declare(intake, scope, TurnPorts.Text(registry, scope));
    }

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>换一份绑定到该回合的载体。</summary>
    public ITool ForTurn(TurnScope scope) => new PortTool(_intake, _registry, scope);

    /// <summary>把契约定义绑上提交该回合端口内容的调用体，有端口清单时写进说明。</summary>
    private static IReadOnlyList<ToolFunction> Declare(PortSubmitter intake, TurnScope? scope, IReadOnlyList<Port>? ports) =>
    [
        ContractToolDefinitions.Port(ports).Bind(arguments =>
            intake.SubmitValues(scope?.Run ?? default, arguments.Text(new ToolName("valuesJson")) ?? string.Empty)),
    ];
}
