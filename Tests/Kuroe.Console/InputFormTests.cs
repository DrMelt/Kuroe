using Kuroe.Console.Views;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>输入表的选择、重印、上下文、凭据遮罩与取消。</summary>
public sealed class InputFormTests
{
    [Fact]
    public void Form_prints_every_field_and_the_filled_values()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("alpha");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("beta");
        output.Input.PushKey(ConsoleKey.Enter);

        IReadOnlyList<string>? values = new InputForm(
            terminal,
            "表单",
            [new FormField("甲"), new FormField("乙")]).Run();

        Assert.NotNull(values);
        Assert.Equal("alpha", values[0]);
        Assert.Equal("beta", values[1]);

        string text = output.Output;
        string[] blocks = text.Split("表单");
        Assert.Equal(5, blocks.Length);
        Assert.Contains("甲", blocks[2]);
        Assert.Contains("alpha", blocks[2]);
        Assert.Contains("乙 · 未填写", blocks[2]);
        Assert.DoesNotContain("输入中", text);
    }

    [Fact]
    public void Form_prints_context_and_the_current_description()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("value");
        output.Input.PushKey(ConsoleKey.Enter);

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
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("token");
        output.Input.PushKey(ConsoleKey.Enter);

        new InputForm(terminal, "凭据", [new FormField("凭据", Secret: true)]).Run();

        string text = output.Output;
        Assert.DoesNotContain("token", text);
        Assert.Contains("已填写", text);
    }

    [Fact]
    public void Form_allows_filling_fields_out_of_order()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.DownArrow);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("beta");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("alpha");
        output.Input.PushKey(ConsoleKey.Enter);

        IReadOnlyList<string>? values = new InputForm(
            terminal,
            "填写",
            [new FormField("甲"), new FormField("乙")]).Run();

        Assert.NotNull(values);
        Assert.Equal(["alpha", "beta"], values);
    }

    [Fact]
    public void Form_allows_rewriting_a_filled_field()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("first");
        output.Input.PushKey(ConsoleKey.DownArrow);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("second");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("beta");
        output.Input.PushKey(ConsoleKey.Enter);

        IReadOnlyList<string>? values = new InputForm(
            terminal,
            "填写",
            [new FormField("甲"), new FormField("乙")]).Run();

        Assert.NotNull(values);
        Assert.Equal(["second", "beta"], values);
    }

    [Fact]
    public void Form_does_not_offer_submit_while_a_field_is_unfilled()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.DownArrow);
        output.Input.PushKey(ConsoleKey.DownArrow);
        output.Input.PushKey(ConsoleKey.Enter);

        Assert.Null(new InputForm(terminal, "填写", [new FormField("甲"), new FormField("乙")]).Run());
        Assert.DoesNotContain("√ 提交", output.Output);
    }

    [Fact]
    public void Form_returns_null_on_escape()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Escape);

        Assert.Null(new InputForm(terminal, "填写", [new FormField("甲"), new FormField("乙")]).Run());
    }

    [Fact]
    public void Form_returns_null_when_a_field_input_is_cancelled()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("alpha");
        output.Input.PushKey(ConsoleKey.Escape);

        Assert.Null(new InputForm(terminal, "填写", [new FormField("甲"), new FormField("乙")]).Run());
    }

    [Fact]
    public void Form_returns_null_on_an_empty_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushKey(ConsoleKey.Enter);

        Assert.Null(new InputForm(terminal, "填写", [new FormField("甲")]).Run());
    }

    [Fact]
    public void Form_prints_the_completed_table_without_the_selection_menu()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("alpha");
        output.Input.PushKey(ConsoleKey.Enter);

        new InputForm(terminal, "填写", [new FormField("甲")]).Run();

        string text = output.Output;
        int submit = text.LastIndexOf("√ 提交", StringComparison.Ordinal);
        Assert.True(submit >= 0, text);
        Assert.Contains("alpha", text[submit..]);
        Assert.DoesNotContain("甲 · 未填写", text[submit..]);
    }

    [Fact]
    public void Form_without_fields_returns_an_empty_result_without_a_menu()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);

        IReadOnlyList<string>? values = new InputForm(
            terminal,
            "填写",
            [],
            [new FormInfo("固定", "值")]).Run();

        Assert.NotNull(values);
        Assert.Empty(values);
        Assert.Contains("固定", output.Output);
        Assert.DoesNotContain("选择填写项", output.Output);
    }
}