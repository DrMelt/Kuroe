using Kuroe.Flow.Shared.Tools;

namespace Kuroe.Node.Llm;

/// <summary>没有工具契约的目录，用于不经过工具装配的流程编译与测试。</summary>
internal sealed class EmptyToolContractCatalog : IToolContractCatalog
{
    /// <summary>共享的空目录。</summary>
    internal static EmptyToolContractCatalog Instance { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<ToolContract> Contracts => [];
}