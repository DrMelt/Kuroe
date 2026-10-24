using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Runtime.Tasks;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>执行侧通知的一行呈现。后台执行的过程不进终端，只在这里报问题。</summary>
internal sealed class ExecutionNotifier(TaskRegistry registry, Terminal terminal)
{
    private readonly TaskRegistry _registry = registry;
    private readonly Terminal _terminal = terminal;

    /// <summary>订阅通知，由对话循环在开始时调用一次。</summary>
    public void Attach() => _registry.Notified += Report;

    private void Report(ExecutionNotice notice)
    {
        string text = notice.Text.EndsWith('。') ? notice.Text : notice.Text + "。";
        Style style = notice.Level switch
        {
            NoticeLevel.Error => Styles.Error,
            NoticeLevel.Warning => Styles.Warning,
            _ => Styles.Hint,
        };

        _terminal.Notice(text, style);
    }
}
