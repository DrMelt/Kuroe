using System.Text;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>带提示的行输入：回车返回文本，Esc 或空行返回 null，凭据按掩码回显。</summary>
internal sealed class LinePrompt(string label, bool secret = false) : TextPrompt(label)
{
    protected override bool Masked => secret;

    protected override bool OnEnter(StringBuilder text, IAnsiConsole console)
    {
        console.WriteLine();

        return true;
    }
}