using Kuroe.Console.Views;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>带框选择的键盘移动与结果。</summary>
public sealed class MenuPromptTests
{
    [Fact]
    public void Menu_prompt_draws_a_frame_and_returns_the_selected_choice()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);

        string picked = terminal.Prompt(new MenuPrompt<string>("选择任务", ["甲", "乙"], value => value, "取消"));

        Assert.Equal("甲", picked);
        Assert.Contains('╭', output.Output);
        Assert.Contains('╰', output.Output);
        Assert.Contains("选择任务", output.Output);
        Assert.Contains("甲", output.Output);
        Assert.Contains("乙", output.Output);
    }

    [Fact]
    public void Menu_prompt_moves_with_arrow_keys()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.DownArrow);
        output.Input.PushKey(ConsoleKey.Enter);

        string picked = terminal.Prompt(new MenuPrompt<string>("选择任务", ["甲", "乙"], value => value, "取消"));

        Assert.Equal("乙", picked);
    }

    [Fact]
    public void Menu_prompt_returns_the_cancel_choice_on_escape()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Escape);

        string picked = terminal.Prompt(new MenuPrompt<string>("选择任务", ["甲", "乙"], value => value, "取消"));

        Assert.Equal("取消", picked);
    }
}