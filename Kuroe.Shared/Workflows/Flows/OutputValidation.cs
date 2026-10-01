namespace Kuroe.Shared.Workflows.Flows;

/// <summary>输出校验谓词：决定模型产出是否合格。</summary>
public enum ValidationPredicate
{
    /// <summary>产出非空。</summary>
    NonEmpty,

    /// <summary>产出包含指定文本。</summary>
    TextContains,

    /// <summary>产出不包含指定文本。</summary>
    TextNot,

    /// <summary>产出与指定文本完全一致。</summary>
    TextEquals,

    /// <summary>产出匹配指定正则。</summary>
    Pattern,
}

/// <summary>执行节点的输出校验：收口时校验模型产出，不通过则节点阻塞待返工。</summary>
public sealed record OutputValidation(ValidationPredicate Predicate, string? Argument);
