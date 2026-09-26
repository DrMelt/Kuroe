namespace Kuroe.Shared.Workflows.Flows;

/// <summary>叶子的产出契约：交回什么。执行引擎据此决定收口方式与契约工具。</summary>
public enum NodeOutput
{
    /// <summary>普通文本产出。</summary>
    Plain,

    /// <summary>拆分目标，交回可独立实施的条目列表。</summary>
    Plan,

    /// <summary>检查实施产出，交回通过与否的结论。</summary>
    Review,
}