namespace Kuroe.Flow.Compilation.Documents.Shared;

/// <summary>一组 From 连接。</summary>
public sealed record FromGroupDocument(IReadOnlyList<FromLinkDocument> Links);