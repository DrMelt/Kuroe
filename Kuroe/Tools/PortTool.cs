using Kuroe.Executions.Tools;
using Kuroe.Executions.Turns;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Workflows.TaskExecution.Tasks;

namespace Kuroe.Tools;

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
    private IReadOnlyList<PortName>? PortsOf(TurnScope scope) =>
        _registry.FindRun(scope.Run) is not { IsError: false } found
        || found.Value.Context.OutputPorts is not { Count: > 0 } ports
            ? null
            : ports;

    /// <summary>声明绑定到该回合上的提交函数。有端口清单时把端口名写进描述，模型按声明的键值提交。</summary>
    private static IReadOnlyList<ToolFunction> Declare(PortSubmitter intake, TurnScope? scope, IReadOnlyList<PortName>? ports)
    {
        string listed = ports is null ? string.Empty : $"本节点声明端口：{string.Join('、', ports.Select(port => port.Value))}。";

        return
        [
            new ToolFunction(ToolName.ContractPortValues,
                $"提交本执行节点命名输出端口的内容。{listed} valuesJson 是端口名到文本的 JSON 对象，端口必须与声明一一对应。",
                [new ToolParameter(new ToolName("valuesJson"), "端口名到文本的 JSON 对象，端口必须与声明一一对应", Required: true)],
                arguments => intake.SubmitValues(scope, arguments.Text(new ToolName("valuesJson")) ?? string.Empty),
                ToolPath.ContractPortValues),
        ];
    }
}
