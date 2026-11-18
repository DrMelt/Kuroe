using Kuroe.Console.Views;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>多行输入：回车换行、终止行提交、取消、行内退格与行首合并。</summary>
public sealed class MultilinePromptTests
{
    private const int Width = 80;

    [Fact(DisplayName = "回车插入换行终止行提交")]
    public void Enter_inserts_a_line_break_and_the_terminator_submits()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushText("first");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter("second");
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal("first\nsecond", terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "终止行只有独占整行才提交")]
    public void Terminator_only_submits_when_it_occupies_the_whole_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushText("value");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter(";;tail");
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal("value\n;;tail", terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "两侧有空格的终止行不提交")]
    public void Terminator_with_surrounding_spaces_does_not_submit()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushText("value");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter(" ;; ");
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal("value\n ;; ", terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "文本中的空行保留")]
    public void Blank_lines_inside_the_text_are_kept()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushText("first");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushText("second");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal("first\n\nsecond", terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "空输入上的终止行返回空")]
    public void Terminator_on_an_empty_input_returns_null()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Null(terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "返回键返回空并丢弃文本")]
    public void Escape_returns_null_and_discards_the_text()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushText("discarded");
        output.Input.PushKey(ConsoleKey.Escape);

        Assert.Null(terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "退格删除当前行最后一个字符")]
    public void Backspace_removes_the_last_character_of_the_current_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushText("first");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushText("x");
        output.Input.PushKey(ConsoleKey.Backspace);
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal("first", terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "空行退格并入上一行")]
    public void Backspace_on_an_empty_line_joins_the_previous_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushText("first");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushKey(ConsoleKey.Backspace);
        output.Input.PushTextWithEnter("x");
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal("firstx", terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "退格依次并入连续空行")]
    public void Backspace_joins_repeated_empty_lines()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushText("first");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushKey(ConsoleKey.Backspace);
        output.Input.PushKey(ConsoleKey.Backspace);
        output.Input.PushTextWithEnter("x");
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal("firstx", terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "退格并入选宽字符结尾的行")]
    public void Backspace_joins_a_line_ending_with_a_wide_character()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(new ConsoleKeyInfo('中', ConsoleKey.A, false, false, false));
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushKey(ConsoleKey.Backspace);
        output.Input.PushTextWithEnter("x");
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal("中x", terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "输入开头的退格不生效")]
    public void Backspace_at_the_start_of_the_input_does_nothing()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Backspace);
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Null(terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "退格并入把光标移到上一行末尾")]
    public void Backspace_join_moves_the_cursor_to_the_previous_line_end()
    {
        Terminal terminal = NewRecordingTerminal(out TestConsole output, out CursorRecordingConsole recording);
        output.Input.PushText("first");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushKey(ConsoleKey.Backspace);
        output.Input.PushTextWithEnter("x");
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal("firstx", terminal.AskMultiline("p"));
        var item = Assert.Single(recording.Moves);
        Assert.Equal((CursorDirection.Up, 1), item);
    }

    [Fact(DisplayName = "退格并入按换行前的行数计算行")]
    public void Backspace_join_counts_the_rows_of_a_wrapped_previous_line()
    {
        Terminal terminal = NewRecordingTerminal(out TestConsole output, out CursorRecordingConsole recording);
        string line = new('a', Width + 1);
        output.Input.PushText(line);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushKey(ConsoleKey.Backspace);
        output.Input.PushTextWithEnter("x");
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal($"{line}x", terminal.AskMultiline("p"));
        var item = Assert.Single(recording.Moves);
        Assert.Equal((CursorDirection.Up, 2), item);
    }

    [Fact(DisplayName = "退格并入空的起始行")]
    public void Backspace_joins_an_empty_first_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushKey(ConsoleKey.Backspace);
        output.Input.PushTextWithEnter("x");
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal("x", terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "退格删除当前行的宽字符")]
    public void Backspace_removes_a_wide_character_on_the_current_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushKey(new ConsoleKeyInfo('中', ConsoleKey.A, false, false, false));
        output.Input.PushKey(new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false));
        output.Input.PushKey(ConsoleKey.Backspace);
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal("中", terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "退格把代理对作为一个字符删除")]
    public void Backspace_removes_a_surrogate_pair_as_one_character()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        const string emoji = "😀";
        output.Input.PushKey(new ConsoleKeyInfo(emoji[0], ConsoleKey.A, false, false, false));
        output.Input.PushKey(new ConsoleKeyInfo(emoji[1], ConsoleKey.A, false, false, false));
        output.Input.PushKey(ConsoleKey.Backspace);
        output.Input.PushTextWithEnter("x");
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal("x", terminal.AskMultiline("回答 >"));
    }

    [Fact(DisplayName = "多行提示把标签写在独立行")]
    public void Multiline_prompt_writes_the_label_on_its_own_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Input.PushText("first");
        output.Input.PushKey(ConsoleKey.Enter);
        output.Input.PushTextWithEnter(MultilinePrompt.Terminator);

        Assert.Equal("first", terminal.AskMultiline("回答 >"));

        string text = output.Output.Replace("\r\n", "\n");
        Assert.Contains("回答 >\nfirst", text);
    }

    /// <summary>记录光标移动的终端，宽度固定为 80 列。</summary>
    private static Terminal NewRecordingTerminal(out TestConsole output, out CursorRecordingConsole recording)
    {
        output = new TestConsole();
        output.Profile.Capabilities.Ansi = false;
        output.Profile.Width = Width;
        recording = new CursorRecordingConsole(output);

        return new Terminal(recording, recording);
    }
}