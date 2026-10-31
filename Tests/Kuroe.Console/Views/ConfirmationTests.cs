using Kuroe.Console.Views;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>需要再确认的动作：默认项、确认项与取消。</summary>
public sealed class ConfirmationTests
{
    [Fact]
    public void Confirmation_keeps_the_current_state_by_default()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);

        bool confirmed = Confirmation.Ask(terminal, "退出？", "确认", "返回");

        Assert.False(confirmed);
    }

    [Fact]
    public void Confirmation_returns_true_only_for_the_confirm_choice()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.DownArrow);
        output.Input.PushKey(ConsoleKey.Enter);

        bool confirmed = Confirmation.Ask(terminal, "退出？", "确认", "返回");

        Assert.True(confirmed);
    }

    [Fact]
    public void Confirmation_keeps_the_current_state_on_escape()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Escape);

        bool confirmed = Confirmation.Ask(terminal, "退出？", "确认", "返回");

        Assert.False(confirmed);
    }
}