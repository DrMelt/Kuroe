namespace Kuroe.Workflows;

/// <summary>按条目展开还是整步一个 agent。</summary>
public enum StepScope
{
    /// <summary>整步一个 agent。</summary>
    Single,

    /// <summary>规划交回的每个条目各一个 agent。</summary>
    PerItem,
}
