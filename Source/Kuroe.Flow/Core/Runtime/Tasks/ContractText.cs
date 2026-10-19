using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution.Tasks;
using Kuroe.Flow.RuntimeGraph;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>契约工具回执的模型可见文本：运行中的待办与本轮状态，各契约入口共用一份措辞。要求调用方持有任务 Gate。</summary>
static class ContractText
{
    /// <summary>本轮还需交回的产出，未缺时说明已齐备。</summary>
    public static string Needs(WorkTask task, Execution execution)
    {
        TaskRuntime.ExecutionShortfall shortfall = TaskRuntime.Shortfall(task.Runtime.Node(execution), execution.Id);

        List<string> missing = [];
        if (shortfall.Ports.Count > 0)
        {
            missing.Add($"端口{string.Join('、', shortfall.Ports.Select(name => $"「{name}」"))}");
        }

        return missing.Count == 0
            ? "本轮产出已齐备。"
            : $"本轮仍需交回：{string.Join('；', missing)}。";
    }

    /// <summary>仍需交回的产出与本轮状态，附在契约工具的回执后。</summary>
    public static string Outstanding(WorkTask task, Execution execution) => $"{Needs(task, execution)}{StatusLine(task, execution)}";

    /// <summary>本轮状态的写法：未提交时给出提交提示，已提交时给出结论。</summary>
    private static string StatusLine(WorkTask task, Execution execution) =>
        task.Runtime.Node(execution).Verdict(execution.Id) switch
        {
            null => $"本轮状态未提交，结束时用 {ContractTools.Status} 声明 completed 或 blocked。",
            { Blocked: true, Reason: { Length: > 0 } reason } => $"本轮状态：blocked，{reason}。",
            { Blocked: true } => "本轮状态：blocked。",
            _ => "本轮状态：completed。",
        };
}
