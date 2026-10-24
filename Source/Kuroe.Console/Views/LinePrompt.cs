using System.Text;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>带提示的行输入：回车返回文本，Esc 或空行返回 null，凭据按掩码回显。</summary>
internal sealed class LinePrompt(string label, bool secret = false) : IPrompt<string?>
{
    private const char Mask = '*';

    public string? Show(IAnsiConsole console)
    {
        StringBuilder text = new();
        console.Write(label.TrimEnd() + " ", Style.Plain);

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
                    console.WriteLine();

                    return text.Length == 0 ? null : text.ToString();

                case ConsoleKey.Escape:
                    console.WriteLine();

                    return null;

                case ConsoleKey.Backspace:
                    if (text.Length > 0)
                    {
                        char last = text[^1];
                        text.Length--;
                        Erase(console, DisplayWidth(last));
                    }

                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        text.Append(key.KeyChar);
                        console.Write(secret ? Mask.ToString() : key.KeyChar.ToString(), Style.Plain);
                    }

                    break;
            }
        }
    }

    public Task<string?> ShowAsync(IAnsiConsole console, CancellationToken cancellationToken) =>
        Task.FromResult(Show(console));

    /// <summary>回显占用的终端列数。掩码固定占一列。</summary>
    private int DisplayWidth(char value) => secret ? 1 : value.GetCellWidth();

    /// <summary>退回并覆盖已经回显的字符。</summary>
    private static void Erase(IAnsiConsole console, int width)
    {
        for (int i = 0; i < width; i++)
        {
            console.Write("\b \b");
        }
    }
}