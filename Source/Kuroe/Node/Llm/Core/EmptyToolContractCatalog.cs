using Kuroe.Tool;

namespace Kuroe.Node.Llm;

/// <summary>没有工具契约的目录。</summary>
internal sealed class EmptyToolContractCatalog : IToolContractCatalog
{
    /// <summary>共享的空目录。</summary>
    internal static EmptyToolContractCatalog Instance { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<ToolContract> Contracts => [];
}