namespace Kuroe.Flow.Presentation;

/// <summary>展示用端口：名称、种类、文本与触发位。</summary>
public sealed record FlowPortView(
    string Name,
    string Kind,
    string? Text,
    bool Fires = true);