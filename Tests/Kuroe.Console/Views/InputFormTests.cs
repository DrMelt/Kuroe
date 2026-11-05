using Kuroe.Console.Views;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

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

        new InputForm(terminal, "凭据", [new FormField("凭据", Kind: FieldKind.Secret)]).Run();

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
    public void Form_reads_a_multiline_field_until_the_terminator_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushText("first");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushText("second");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);
        output.Input.PushKey(ConsoleKey.Enter);

        IReadOnlyList<string>? values = new InputForm(
            terminal,
            "填写",
            [new FormField("回答", Kind: FieldKind.Multiline)]).Run();

        Assert.NotNull(values);
        Assert.Equal("first\n\nsecond", values[0]);
        Assert.DoesNotContain(MultilinePrompt.Terminator, values[0]);
        Assert.Contains("多行输入", output.Output);
        Assert.Contains("单独一行 ;; 提交", output.Output);

        string table = output.Output[output.Output.LastIndexOf("填写", StringComparison.Ordinal)..];
        Assert.Contains("first", table);
        Assert.Contains("second", table);
    }

    [Fact]
    public void Form_returns_null_when_a_multiline_field_is_cancelled()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushText("draft");
        output.Input.PushKey(ConsoleKey.Escape);

        Assert.Null(new InputForm(
            terminal,
            "填写",
            [new FormField("回答", Kind: FieldKind.Multiline)]).Run());
    }

    [Fact]
    public void Form_starts_with_initial_values_and_submits_them()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);

        IReadOnlyList<string>? values = new InputForm(
            terminal,
            "填写",
            [new FormField("分支", Initial: "main")]).Run();

        Assert.Equal(["main"], values);
        Assert.Contains("main", output.Output);
    }

    [Fact]
    public void Form_submits_when_only_required_fields_are_filled()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("alpha");
        output.Input.PushKey(ConsoleKey.Enter);

        IReadOnlyList<string>? values = new InputForm(
            terminal,
            "填写",
            [new FormField("甲"), new FormField("乙", Optional: true)]).Run();

        Assert.Equal(["alpha", ""], values);
        Assert.Contains("乙 · 可留空", output.Output);
    }

    [Fact]
    public void Form_allows_rewriting_an_initial_value()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.DownArrow);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("dev");
        output.Input.PushKey(ConsoleKey.Enter);

        IReadOnlyList<string>? values = new InputForm(
            terminal,
            "填写",
            [new FormField("分支", Initial: "main")]).Run();

        Assert.Equal(["dev"], values);
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