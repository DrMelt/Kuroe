namespace Kuroe.Shared.Workflows.Flows;

/// <summary>容器节点的执行模式。</summary>
public enum ContainerMode
{
    /// <summary>子节点顺序执行，容器只提供作用域。</summary>
    Sequential,

    /// <summary>子叶子是并行分支，按上游分配方案的条目归属展开与推进。</summary>
    Parallel,
}