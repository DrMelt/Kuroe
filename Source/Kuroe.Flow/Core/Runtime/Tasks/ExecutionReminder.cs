using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Tasks;
using Kuroe.Flow.RuntimeGraph;
using Kuroe.Flow.RuntimeNodes;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>本轮产出契约的提示：声明了输出端口却未交回时，给出写给模型的补交要求。</summary>
internal sealed class ExecutionReminder(TaskRegistry registry) : IExecutionContract
{
    /// <summary>还缺交回内容时给出补交要求，齐备时为空。</summary>
    public string? Reminder(IExecution execution)
    {
        if (registry.Find(execution.Input.Task) is not { IsError: false } found)
        {
            return null;
        }

        WorkTask task = found.Value;
        lock (task.Gate)
        {
            RuntimeExecutable node = task.Runtime.Node(execution);

            // 已声明无法完成，缺项是它主动留下的，不再追问
            if (node.Verdict(execution.Id) is { Blocked: true })
            {
                return null;
            }

            TaskRuntime.ExecutionShortfall shortfall = TaskRuntime.Shortfall(node, execution.Id);
            if (shortfall.None)
            {
                return null;
            }

            string ports = string.Join('、', shortfall.Ports.Select(name => $"「{name}」"));

            return $"本轮还缺交回：输出端口{ports}，请调用 {ContractTools.Port} 提交。";
        }
    }
}
