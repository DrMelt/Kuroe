using Kuroe.Flow.Execution;
using Kuroe.Flow.Shared.Display;

namespace Kuroe.Cli.Views;

/// <summary>任务与执行的纯文本呈现：阻塞失败说明与执行用量。</summary>
internal static class PlainTaskView
{
    /// <summary>列出任务级与执行级的失败说明，写到 stderr。</summary>
    public static void Failures(CliOutput output, TaskSnapshot task)
    {
        foreach (JournalEntry entry in task.Journal.Where(entry => entry.Level == JournalLevel.Error))
        {
            output.ErrorLine($"任务记录：{JournalText.Normalize(entry.Text)}");
        }

        foreach (ExecutionSnapshot execution in task.Executions)
        {
            foreach (string failure in execution.Failures)
            {
                output.ErrorLine($"节点 {execution.Request.Target}：{failure}");
            }
        }
    }

    /// <summary>按执行给出请求次数、上下文与 token 条目，没有模型请求的执行不输出。</summary>
    public static void Usage(CliOutput output, TaskSnapshot task)
    {
        foreach (ExecutionSnapshot execution in task.Executions)
        {
            if (execution.Usage.RequestCount == 0)
            {
                continue;
            }

            output.Hint($"节点 {execution.Request.Target} 第 {execution.Request.Attempt} 轮"
                + $" · 请求 {InfoLabels.Requests(execution.Usage)}"
                + $" · 上下文 {InfoLabels.Context(execution.Usage)}");
            foreach ((string label, string value) in InfoLabels.TokenEntries(execution.Usage))
            {
                output.Hint($"  {label} {value}");
            }
        }
    }
}