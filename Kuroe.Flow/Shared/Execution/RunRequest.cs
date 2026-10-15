using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>引擎为一次执行给出的本轮材料：执行说明、接口说明与契约能力，与类型参数无关。</summary>
public sealed record RunMaterials
{
    /// <summary>本次负责的条目与第几轮的说明，每行一条，无内容时为空。</summary>
    public IReadOnlyList<string> Steps { get; init; } = [];

    /// <summary>本轮接口说明：本节点的输入端口、需交回的产出与状态工具。</summary>
    public string Briefing { get; init; } = string.Empty;

    /// <summary>引擎按输出端口附上的契约能力路径。</summary>
    public IReadOnlyList<CapabilityPath> ContractTools { get; init; } = [];
}

/// <summary>一次执行交给模型侧的请求内容。</summary>
public sealed record RunRequest
{
    /// <summary>本轮要做的事，随第一条消息发出。</summary>
    public string Instruction { get; init; } = string.Empty;

    /// <summary>系统指令，接在请求最前，没有内容时为空。</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>本轮可用的能力路径白名单。</summary>
    public IReadOnlyList<CapabilityPath> Tools { get; init; } = [];

    /// <summary>未声明模型侧语义时的请求内容：材料原样成为请求。</summary>
    public static RunRequest From(RunMaterials materials) => new()
    {
        Instruction = string.Join('\n', materials.Steps),
        SystemPrompt = materials.Briefing.Length == 0 ? null : materials.Briefing,
        Tools = materials.ContractTools,
    };
}