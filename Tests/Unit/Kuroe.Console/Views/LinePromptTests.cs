using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>行输入的提交、取消、退格与掩码回显。</summary>
public sealed class LinePromptTests
{
    [Fact(DisplayName = "行提示在回车时返回文本")]
    public void Line_prompt_returns_the_text_on_enter()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushTextWithEnter("patch");

        Assert.Equal("patch", terminal.Ask("Kuroe >"));
        Assert.Contains("Kuroe >", output.Output);
        Assert.Contains("patch", output.Output);
    }

    [Fact(DisplayName = "行提示在返回键返回空")]
    public void Line_prompt_returns_null_on_escape()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushText("abc");
        output.Input.PushKey(ConsoleKey.Escape);

        Assert.Null(terminal.Ask("Kuroe >"));
    }

    [Fact(DisplayName = "行提示在空行返回空")]
    public void Line_prompt_returns_null_on_an_empty_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);

        Assert.Null(terminal.Ask("Kuroe >"));
    }

    [Fact(DisplayName = "行提示退格删除最后一个字符")]
    public void Line_prompt_backspace_removes_the_last_character()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushText("abc");
        output.Input.PushKey(ConsoleKey.Backspace);
        output.Input.PushTextWithEnter("d");

        Assert.Equal("abd", terminal.Ask("Kuroe >"));
    }

    [Fact(DisplayName = "行提示按纯文本写出标签")]
    public void Line_prompt_writes_the_label_without_markup_parsing()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushTextWithEnter("ok");

        Assert.Equal("ok", terminal.Ask("[red]标签[/]"));
        Assert.Contains("[red]标签[/]", output.Output);
    }

    [Fact(DisplayName = "秘密行提示掩码回显")]
    public void Secret_line_prompt_masks_the_echo()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushTextWithEnter("token");

        Assert.Equal("token", terminal.AskSecret("凭据："));
        Assert.Contains("*****", output.Output);
        Assert.DoesNotContain("token", output.Output);
    }
}