namespace Kuroe.Tool;

/// <summary>模型可调用的函数名，工具注册按它排重，模型按它调用。展示即原文。</summary>
public readonly record struct ToolName(string Value)
{
    /// <summary>函数名的文本形式。</summary>
    public override string ToString() => Value;
}
