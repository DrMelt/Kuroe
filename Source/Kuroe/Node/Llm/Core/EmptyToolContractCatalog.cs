using Kuroe.Tool;

namespace Kuroe.Node.Llm;

/// <summary>没有工具契约的目录。</summary>
internal sealed class EmptyToolContractCatalog : IToolContractCatalog
{
    /// <inheritdoc/>
    public IReadOnlyList<ToolContract> Contracts => [];
}