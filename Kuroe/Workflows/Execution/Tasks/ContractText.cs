using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Execution.Tasks;

/// <summary>契约工具的模型可见文本：端口声明的写法与运行中的待办提示，各契约入口共用一份措辞。要求调用方持有任务 Gate。</summary>
static class ContractText
{
    /// <summary>端口声明的清单，按声明序，有说明写 端口名：说明。</summary>
    public static string List(IEnumerable<Port> ports) => string.Join('、', ports.Select(Describe));

    /// <summary>单个端口的写法：有说明写 端口名：说明，未写说明只写端口名。</summary>
    public static string Describe(Port port) =>
        port.Description is { Length: > 0 } description ? $"{port.Name}：{description}" : port.Name.Value;

    /// <summary>本轮还需交回的产出，未缺时说明已齐备。</summary>
    public static string Needs(WorkTask task, Run run)
    {
        TaskRuntime.RunShortfall shortfall = task.Runtime.Shortfall(run);

        List<string> missing = [];
        if (shortfall.Items)
        {
            missing.Add("条目拆分");
        }

        if (shortfall.Ports.Count > 0)
        {
            missing.Add($"端口{string.Join('、', shortfall.Ports.Select(name => $"「{name}」"))}");
        }

        return missing.Count == 0
            ? "本轮产出已齐备。"
            : $"本轮仍需交回：{string.Join('；', missing)}。";
    }

    /// <summary>仍需交回的产出与本轮状态，附在契约工具的回执后。</summary>
    public static string Outstanding(WorkTask task, Run run) => $"{Needs(task, run)}{StatusLine(task, run)}";

    /// <summary>本轮状态的写法：未提交时给出提交提示，已提交时给出结论。</summary>
    private static string StatusLine(WorkTask task, Run run) =>
        task.VerdictFor(run.Context.NodeIndex, run.Context.ItemIndex) switch
        {
            null => $"本轮状态未提交，结束时用 {ToolName.ContractStatus} 声明 completed 或 blocked。",
            { Blocked: true, Reason: { Length: > 0 } reason } => $"本轮状态：blocked，{reason}。",
            { Blocked: true } => "本轮状态：blocked。",
            _ => "本轮状态：completed。",
        };
}
