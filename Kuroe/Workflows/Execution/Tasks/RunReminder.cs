using Kuroe.Executions.Runs;

namespace Kuroe.Workflows.Execution.Tasks;

/// <summary>本轮产出契约的提示：声明了输出端口或条目拆分却未交回时，给出写给模型的补交要求。</summary>
internal sealed class RunReminder(TaskRegistry registry) : IRunContract
{
    /// <summary>还缺交回内容时给出补交要求，齐备时为空。</summary>
    public string? Reminder(Run run)
    {
        if (registry.Find(run.Context.Task) is not { IsError: false } found)
        {
            return null;
        }

        WorkTask task = found.Value;
        lock (task.Gate)
        {
            // 已声明无法完成，缺项是它主动留下的，不再追问
            if (task.Runtime.Instance(run).Verdict(run.Id) is { Blocked: true })
            {
                return null;
            }

            TaskRuntime.RunShortfall shortfall = task.Runtime.Shortfall(task.Runtime.Instance(run), run);
            if (shortfall.None)
            {
                return null;
            }

            List<string> missing = [];
            if (shortfall.Items)
            {
                missing.Add($"条目拆分，请调用 {ContractTools.WorkItems.Name} 提交");
            }

            if (shortfall.Ports.Count > 0)
            {
                string ports = string.Join('、', shortfall.Ports.Select(name => $"「{name}」"));
                missing.Add($"输出端口{ports}，请调用 {ContractTools.Port.Name} 提交");
            }

            return $"本轮还缺交回：{string.Join('；', missing)}。";
        }
    }
}
