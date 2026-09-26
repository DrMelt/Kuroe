namespace Kuroe.Shared.Workflows.Flows;

/// <summary>拆分产出被消费的叶子范围。段入口按拆分的条目批量建单元，段内各叶由单元推进。
/// 单分支段内叶子顺序执行，并行段内各分支叶子并行推进并跳过其他分支。</summary>
public sealed record UnitSegment(
    int Source,
    int Start,
    int Exit,
    IReadOnlyList<int> BranchLeaves)
{
    /// <summary>并行段：段内叶子是分支，单元只在其归属分支上执行。</summary>
    public bool IsParallel => BranchLeaves.Count > 0;
}