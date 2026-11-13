using Kuroe.Flow.Execution.Shared;

namespace Kuroe.Tool.Contract;

/// <summary>契约工具源：按执行创建交回、上报与状态查询载体。</summary>
public sealed class ContractToolSource(
    IExecutionPortSubmitter ports,
    IExecutionCannotCompleteSubmitter cannotComplete,
    IExecutionStatusReader status) : IToolSource
{
    /// <summary>契约工具的全部定义，供装配时先占位再按容器创建源。</summary>
    public static IReadOnlyList<ToolDefinition> AllDefinitions { get; } =
    [
        ContractToolDefinitions.Port(null),
        ContractToolDefinitions.CannotComplete(),
        ContractToolDefinitions.Status(),
    ];

    /// <inheritdoc/>
    public IReadOnlyList<ToolDefinition> Definitions => AllDefinitions;

    /// <inheritdoc/>
    public IReadOnlyList<ITool> Create(ToolContext context) =>
    [
        new PortTool(ports, context.Execution, ContractPorts.DeclaredPorts(context.Ports.Outputs)),
        new CannotCompleteTool(cannotComplete, context.Execution),
        new StatusTool(status, context.Execution),
    ];
}