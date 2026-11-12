namespace Kuroe.Flow.Shared.RuntimeGraph.Nodes;

/// <summary>一次发布及其发生时刻。</summary>
public sealed record PublishedPorts(Publication Publication, DateTimeOffset At);