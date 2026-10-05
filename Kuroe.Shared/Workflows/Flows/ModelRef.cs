namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程内模型选择的引用名，执行节点按它选模型。展示即原文。</summary>
public readonly record struct ModelRef(string Value)
{
    /// <summary>模型选择名的文本形式。</summary>
    public override string ToString() => Value;
}
