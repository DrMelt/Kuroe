using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Runtime.Tasks;

namespace Kuroe.Tool.Contract;

/// <summary>声明输出端口的执行节点交回命名端口产出的通道。载体按执行换一份，提交时才知道是哪个执行在交。</summary>
public sealed class PortTool : IScopedTool
{
    private readonly PortSubmitter _intake;
    private readonly TaskRegistry _registry;

    /// <summary>容器装配用的载体，尚未绑定执行。</summary>
    public PortTool(PortSubmitter intake, TaskRegistry registry)
    {
        _intake = intake;
        _registry = registry;
        Functions = Declare(intake, null, null);
    }

    private PortTool(PortSubmitter intake, TaskRegistry registry, ExecutionId execution)
    {
        _intake = intake;
        _registry = registry;
        Functions = Declare(intake, execution, ContractPorts.DeclaredTextPorts(registry, execution));
    }

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>换一份绑定到该执行的载体。</summary>
    public ITool ForExecution(ExecutionId execution) => new PortTool(_intake, _registry, execution);

    /// <summary>把契约定义绑上提交该执行端口内容的调用体，有端口清单时写进说明。</summary>
    private static IReadOnlyList<ToolFunction> Declare(PortSubmitter intake, ExecutionId? execution, IReadOnlyList<Port>? ports) =>
    [
        ContractToolDefinitions.Port(ports).Bind(arguments =>
            intake.SubmitValues(execution ?? default, arguments.Text(new ToolName("valuesJson")) ?? string.Empty)),
    ];
}
