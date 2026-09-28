namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程内模型选择的引用名，执行节点按它取模型配置。展示即原文。</summary>
public readonly record struct ModelRef(string Value)
{
    /// <summary>配置名的文本形式。</summary>
    public override string ToString() => Value;
}