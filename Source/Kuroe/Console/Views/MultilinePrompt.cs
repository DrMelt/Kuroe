using System.Text;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>带提示的多行输入：回车换行，单独一行 ;; 提交，Esc 或空内容返回 null。</summary>
internal sealed class MultilinePrompt(string label) : TextPrompt(label)
{
    /// <summary>独占一行输入时表示提交的终止行。</summary>
    public const string Terminator = ";;";

    protected override bool LabelOnOwnLine => true;

    protected override bool OnEnter(StringBuilder text, IAnsiConsole console)
    {
        int start = LineStart(text, text.Length);
        string current = text.ToString(start, text.Length - start);
        if (current != Terminator)
        {
            text.Append('\n');
            console.WriteLine();

            return false;
        }

        // 去掉终止行与它前面的换行。
        text.Length = start > 0 ? start - 1 : 0;
        console.WriteLine();

        return true;
    }
}