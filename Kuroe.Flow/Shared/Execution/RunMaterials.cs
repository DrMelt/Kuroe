using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>引擎为一次执行给出的本轮材料：执行说明与接口说明，与类型专属声明无关。</summary>
public sealed record RunMaterials
{
    /// <summary>本次负责的条目与第几轮的说明，每行一条，无内容时为空。</summary>
    public IReadOnlyList<string> Steps { get; init; } = [];

    /// <summary>本轮接口说明：本节点的输入端口、需交回的产出与状态工具。</summary>
    public string Briefing { get; init; } = string.Empty;
}