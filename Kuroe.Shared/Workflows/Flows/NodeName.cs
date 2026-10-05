namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程图里的节点名。流程内唯一，展示即原文。</summary>
public readonly record struct NodeName(string Value)
{
    /// <summary>前台对话在过程记录里的节点名。</summary>
    public static NodeName Dialogue { get; } = new("对话");

    /// <summary>节点名的文本形式。</summary>
    public override string ToString() => Value;
}
