namespace Kuroe.Shared.Workflows.Flows;

/// <summary>叶子整叶一个 agent 还是按规划条目各派一个。</summary>
public enum NodeMode
{
    /// <summary>整叶一个 agent。</summary>
    Single,

    /// <summary>规划交回的每个条目各一个 agent。</summary>
    PerItem,
}