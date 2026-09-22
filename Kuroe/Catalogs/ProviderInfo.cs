namespace Kuroe.Catalogs;

/// <summary>目录中的一个提供商。Endpoint 是规范化后的端点文本，与录入时的写法可以不同。
/// 凭据永不为空，只区分是否为导出占位符，原文不经此处。</summary>
public sealed record ProviderInfo(string Name, string Endpoint, bool HasPlaceholderKey);
