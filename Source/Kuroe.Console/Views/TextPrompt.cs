using System.Text;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>读文本的按键循环：标签、回显、退格与取消由这里统一处理，Enter 的语义交给实现。</summary>
internal abstract class TextPrompt(string label) : IPrompt<string?>
{
    private const char Mask = '*';

    /// <summary>回显是否使用掩码。掩码只适用于不含换行的输入。</summary>
    protected virtual bool Masked => false;

    /// <summary>输入是否从标签下一行开始。</summary>
    protected virtual bool LabelOnOwnLine => false;

    public string? Show(IAnsiConsole console)
    {
        StringBuilder text = new();
        string head = LabelOnOwnLine ? label.TrimEnd() : label.TrimEnd() + " ";
        console.Write(head, Style.Plain);
        if (LabelOnOwnLine)
        {
            console.WriteLine();
        }

        while (true)
        {
            ConsoleKeyInfo? pressed = console.Input.ReadKey(true);
            if (pressed is null)
            {
                return null;
            }

            ConsoleKeyInfo key = pressed.Value;
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    if (OnEnter(text, console))
                    {
                        return text.Length == 0 ? null : text.ToString();
                    }

                    break;

                case ConsoleKey.Escape:
                    console.WriteLine();

                    return null;

                case ConsoleKey.Backspace:
                    Backspace(text, console);
                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        text.Append(key.KeyChar);
                        console.Write(Masked ? Mask.ToString() : key.KeyChar.ToString(), Style.Plain);
                    }

                    break;
            }
        }
    }

    public Task<string?> ShowAsync(IAnsiConsole console, CancellationToken cancellationToken) =>
        Task.FromResult(Show(console));

    /// <summary>退格：行首退格与上一行合并，其余删除当前行最后一个字符。</summary>
    private void Backspace(StringBuilder text, IAnsiConsole console)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (text[^1] == '\n')
        {
            JoinLine(text, console);

            return;
        }

        int length = LastCharacterLength(text);
        string removed = text.ToString(text.Length - length, length);
        text.Length -= length;
        Erase(console, Masked ? length : removed.GetCellWidth());
    }

    /// <summary>删除末尾换行，把光标移回上一行末尾。</summary>
    private static void JoinLine(StringBuilder text, IAnsiConsole console)
    {
        int end = text.Length - 1;
        int start = LineStart(text, end);
        string previous = text.ToString(start, end - start);

        text.Length = end;
        console.Cursor.MoveUp(PromptLayout.Rows(console.Profile.Width, previous.GetCellWidth()));
        console.Write(previous, Style.Plain);
    }

    /// <summary>end 之前最后一个换行之后的起点。</summary>
    protected static int LineStart(StringBuilder text, int end)
    {
        for (int index = end - 1; index >= 0; index--)
        {
            if (text[index] == '\n')
            {
                return index + 1;
            }
        }

        return 0;
    }

    /// <summary>末尾字符占用的 UTF-16 长度，代理对算一个字符。</summary>
    private static int LastCharacterLength(StringBuilder text) =>
        text.Length >= 2 && char.IsSurrogatePair(text[^2], text[^1]) ? 2 : 1;

    /// <summary>处理 Enter。返回 true 表示提交，由基类产出结果。</summary>
    protected abstract bool OnEnter(StringBuilder text, IAnsiConsole console);

    /// <summary>退回并覆盖已经回显的字符。</summary>
    private static void Erase(IAnsiConsole console, int width)
    {
        for (int i = 0; i < width; i++)
        {
            console.Write("\b \b");
        }
    }
}