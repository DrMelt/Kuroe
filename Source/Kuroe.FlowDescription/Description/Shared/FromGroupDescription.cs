namespace Kuroe.FlowDescription.Description.Shared;

/// <summary>一组 From 连接。</summary>
public sealed record FromGroupDescription(IReadOnlyList<FromLinkDescription> Links);