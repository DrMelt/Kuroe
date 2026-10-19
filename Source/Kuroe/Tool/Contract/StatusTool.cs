using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Runtime.Tasks;

namespace Kuroe.Tool.Contract;

/// <summary>执行节点声明本轮状态的通道：交付完毕或无法完成。载体按执行换一份，声明时才知道是哪个执行在报。</summary>
public sealed class StatusTool : IScopedTool
{
    private readonly StatusSubmitter _intake;
    private readonly TaskRegistry _registry;

    /// <summary>容器装配用的载体，尚未绑定执行。</summary>
    public StatusTool(StatusSubmitter intake, TaskRegistry registry)
    {
        _intake = intake;
        _registry = registry;
        Functions = Declare(intake, null, null);
    }

    private StatusTool(StatusSubmitter intake, TaskRegistry registry, ExecutionId execution)
    {
        _intake = intake;
        _registry = registry;
        Functions = Declare(intake, execution, ContractPorts.DeclaredTextPorts(registry, execution));
    }

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>换一份绑定到该执行的载体。</summary>
    public ITool ForExecution(ExecutionId execution) => new StatusTool(_intake, _registry, execution);

    /// <summary>把契约定义绑上声明该执行状态的调用体，有端口清单时写进说明。</summary>
    private static IReadOnlyList<ToolFunction> Declare(StatusSubmitter intake, ExecutionId? execution, IReadOnlyList<Port>? ports) =>
    [
        ContractToolDefinitions.Status(ports).Bind(arguments => intake.SubmitVerdict(
            execution ?? default,
            arguments.Text(new ToolName("status")),
            arguments.Text(new ToolName("reason")))),
    ];
}
