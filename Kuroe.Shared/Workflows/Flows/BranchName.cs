namespace Kuroe.Shared.Workflows.Flows;

/// <summary>并行段的分支名。展示即原文。</summary>
public readonly record struct BranchName(string Value)
{
    /// <summary>分支名的文本形式。</summary>
    public override string ToString() => Value;
}
