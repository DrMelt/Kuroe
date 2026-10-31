namespace Kuroe.Tool;

/// <summary>从工具定义建立的契约目录，供流程编译校验。</summary>
/// <remarks>按定义声明序建立契约。</remarks>
public sealed class ToolContractCatalog(IEnumerable<ToolDefinition> definitions) : IToolContractCatalog
{
    /// <inheritdoc/>
    public IReadOnlyList<ToolContract> Contracts { get; } =
        [.. definitions.Select(definition => new ToolContract(definition.Path.Value, definition.PathVariables))];
}