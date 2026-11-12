namespace Kuroe.Flow.Presentation;

/// <summary>展示用分组：路径、标签、成员路径、状态与输出端口。</summary>
public sealed record FlowGroupView(
    string Path,
    string Label,
    IReadOnlyList<string> Members)
{
    /// <summary>所属分组的路径，根级分组为空。</summary>
    public string? ParentPath { get; init; }

    /// <summary>分组状态。</summary>
    public string State { get; init; } = "Pending";

    /// <summary>分组输出端口。</summary>
    public IReadOnlyList<FlowPortView> Outputs { get; init; } = [];

    /// <summary>分组的发布次数上限。</summary>
    public int MaxExecutions { get; init; } = int.MaxValue;
}