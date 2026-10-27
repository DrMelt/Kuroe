using Kuroe.Flow.Runtime.Contract;

namespace Kuroe.Tool.Contract;

/// <summary>契约工具源：按执行创建交回、上报与状态查询载体。</summary>
public sealed class ContractToolSource(
    PortSubmitter ports,
    CannotCompleteSubmitter cannotComplete,
    ExecutionStatusReader status) : IToolSource
{
    /// <inheritdoc/>
    public IReadOnlyList<ToolDefinition> Definitions { get; } =
    [
        ContractToolDefinitions.Port(null),
        ContractToolDefinitions.CannotComplete(),
        ContractToolDefinitions.Status(),
    ];

    /// <inheritdoc/>
    public IReadOnlyList<ITool> Create(ToolContext context) =>
    [
        new PortTool(ports, context.Execution, ContractPorts.DeclaredPorts(context.Ports.Outputs)),
        new CannotCompleteTool(cannotComplete, context.Execution),
        new StatusTool(status, context.Execution),
    ];
}