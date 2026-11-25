namespace Kuroe.Host.Ability.Tool.Implementation.Contract;

/// <summary>契约工具源：按执行创建交回、上报与状态查询载体。</summary>
public sealed class ContractToolSource : IToolSource
{
    /// <summary>契约工具的全部定义。</summary>
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
        new PortTool(context.Requests, ContractPorts.DeclaredPorts(context.Ports.Outputs)),
        new CannotCompleteTool(context.Requests),
        new StatusTool(context.Requests, context.Attempt, context.MaxAttempts),
    ];
}