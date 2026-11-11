namespace Kuroe.Flow.Presentation;

/// <summary>展示用分组：路径、标签与成员步骤路径。</summary>
public sealed record FlowGroupView(
    string Path,
    string Label,
    IReadOnlyList<string> Members);