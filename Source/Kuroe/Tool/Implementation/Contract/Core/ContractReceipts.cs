using Kuroe.Flow.Shared.Execution;

namespace Kuroe.Tool.Contract;

/// <summary>契约工具回执的模型可见文本：本轮还需交回的产出，各契约入口共用一份措辞。</summary>
static class ContractReceipts
{
    /// <summary>本轮仍需交回的产出，未缺时说明已齐备。</summary>
    public static string Needs(ExecutionStatus status) =>
        status.OutstandingPorts.Count == 0
            ? "本轮产出已齐备。"
            : $"本轮仍需交回：端口{string.Join('、', status.OutstandingPorts.Select(name => $"「{name}」"))}。";
}