namespace Kuroe.FlowDescription.Description.Shared;

/// <summary>一组 <see cref="NodeDescription.From"/> 连接。</summary>
public sealed record FromGroupDescription(IReadOnlyList<FromLinkDescription> Links);
