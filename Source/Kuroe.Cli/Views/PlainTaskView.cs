using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;

namespace Kuroe.Cli.Views;

/// <summary>任务与执行的纯文本呈现：阻塞失败说明。</summary>
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
}