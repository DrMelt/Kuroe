namespace Kuroe.Shared.Workflows.Flows;

/// <summary>拆分源的固定配置：静态条目、模型补充上限与统一验收文本。</summary>
public sealed record SplitConfig(
    IReadOnlyList<SplitItem>? Items,
    int? ExtrasMax,
    string? Acceptance);

/// <summary>拆分里一条定死的条目，Title 与 Instruction 必填，其余留空视为未提供。</summary>
public sealed record SplitItem(
    string Title,
    string Instruction,
    string Acceptance,
    string? Branch);