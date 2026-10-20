namespace Kuroe.Tool;

/// <summary>名字的原文形式：参数名、工具路径末段的函数名与模型调用名。展示即原文。</summary>
public readonly record struct ToolName(string Value)
{
    /// <summary>名字的文本形式。</summary>
    public override string ToString() => Value;
}
