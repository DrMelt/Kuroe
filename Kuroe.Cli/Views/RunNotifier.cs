using Kuroe.Shared.Workflows;
using Kuroe.Workflows.Tasks;

namespace Kuroe.Cli.Views;

/// <summary>执行侧通知的一行呈现。后台 agent 的过程不进终端，只在这里报状态变化。</summary>
internal sealed class RunNotifier(TaskRegistry registry, Terminal terminal)
{
    private readonly TaskRegistry _registry = registry;
    private readonly Terminal _terminal = terminal;

    /// <summary>订阅通知，由对话循环在开始时调用一次。</summary>
    public void Attach() => _registry.Notified += Report;

    private void Report(ExecutionNotice notice)
    {
        string text = notice.Text.EndsWith('。') ? notice.Text : notice.Text + "。";
        switch (notice.Level)
        {
            case NoticeLevel.Done:
                _terminal.Notice(text, Styles.Success);
                break;

            case NoticeLevel.Warning:
                _terminal.Notice(text, Styles.Warning);
                break;

            case NoticeLevel.Error:
                _terminal.Notice(text, Styles.Error);
                break;

            default:
                _terminal.Notice(text, Styles.Hint);
                break;
        }
    }
}
