using Kuroe.Flow.Shared.Display;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;

namespace Kuroe.Cli.Views;

/// <summary>任务与执行的纯文本呈现：阻塞失败说明与执行用量。</summary>
internal static class PlainTaskView
{
    /// <summary>列出任务级与执行级的失败说明，写到 stderr。</summary>
    public static void Failures(CliOutput output, TaskSnapshot task)
    {
        foreach (JournalEntry entry in task.Journal.Where(entry => entry.Level == JournalLevel.Error))
        {
            output.ErrorLine($"任务记录：{entry.Text}");
        }

        foreach (ExecutableSnapshot executable in task.Executables)
        {
            foreach (ExecutionSnapshot execution in executable.Executions)
            {
                foreach (string failure in execution.Failures)
                {
                    output.ErrorLine($"节点 {executable.Executable.Path.Value}：{failure}");
                }
            }
        }
    }

    /// <summary>按执行给出一行请求次数与 token 用量，没有模型请求的执行不输出。</summary>
    public static void Usage(CliOutput output, TaskSnapshot task)
    {
        foreach (ExecutableSnapshot executable in task.Executables)
        {
            foreach (ExecutionSnapshot execution in executable.Executions)
            {
                if (execution.Usage.RequestCount == 0)
                {
                    continue;
                }

                string line = $"节点 {executable.Executable.Path.Value} 第 {execution.Input.ExecutionCount} 轮"
                    + $" · 请求 {InfoLabels.Requests(execution.Usage)}"
                    + $" · 用量 {InfoLabels.Tokens(execution.Usage.Tokens)}";
                string details = InfoLabels.UsageDetails(execution.Usage);
                output.Hint(details.Length > 0 ? $"{line} · {details}" : line);
            }
        }
    }
}