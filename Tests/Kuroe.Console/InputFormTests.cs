using Kuroe.Console.Views;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>输入表的重印、上下文、凭据遮罩与取消。</summary>
public sealed class InputFormTests
{
    [Fact]
    public void Form_prints_every_field_and_the_filled_values()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushTextWithEnter("alpha");
        output.Input.PushTextWithEnter("beta");

        IReadOnlyList<string>? values = new InputForm(
            terminal,
            "表单",
            [new FormField("甲"), new FormField("乙")]).Run();

        Assert.NotNull(values);
        Assert.Equal("alpha", values[0]);
        Assert.Equal("beta", values[1]);

        string text = output.Output;
        string[] blocks = text.Split("表单");
        Assert.True(blocks.Length >= 4, text);
        Assert.Contains("甲", blocks[2]);
        Assert.Contains("alpha", blocks[2]);
        Assert.Contains("乙 · 输入中", blocks[2]);
        Assert.Contains("输入中", text);
    }

    [Fact]
    public void Form_prints_context_and_the_current_description()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushTextWithEnter("value");

        new InputForm(
            terminal,
            "填写",
            [new FormField("新值", "按 JSON 字面量解析")],
            [new FormInfo("当前值", "旧值")]).Run();

        string text = output.Output;
        Assert.Contains("当前值", text);
        Assert.Contains("旧值", text);
        Assert.Contains("新值：按 JSON 字面量解析", text);
    }

    [Fact]
    public void Form_masks_secret_values()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushTextWithEnter("token");

        new InputForm(terminal, "凭据", [new FormField("凭据", Secret: true)]).Run();

        string text = output.Output;
        Assert.DoesNotContain("token", text);
        Assert.Contains("已填写", text);
    }

    [Fact]
    public void Form_returns_null_on_escape()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushTextWithEnter("alpha");
        output.Input.PushKey(ConsoleKey.Escape);

        Assert.Null(new InputForm(terminal, "填写", [new FormField("甲"), new FormField("乙")]).Run());
    }

    [Fact]
    public void Form_returns_null_on_an_empty_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);

        Assert.Null(new InputForm(terminal, "填写", [new FormField("甲")]).Run());
    }

    [Fact]
    public void Form_prints_the_completed_table_without_the_current_marker()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushTextWithEnter("alpha");

        new InputForm(terminal, "填写", [new FormField("甲")]).Run();

        string text = output.Output;
        int prompt = text.LastIndexOf("甲：", StringComparison.Ordinal);
        Assert.True(prompt >= 0, text);
        Assert.DoesNotContain("甲 · 输入中", text[prompt..]);
    }
}