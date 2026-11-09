using System.Globalization;
using ErrorOr;

namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>按行读取的输出上限：正文总字符上限与单行行长上限。行长含行内容与终止符。</summary>
/// <param name="Characters">正文总字符上限。</param>
/// <param name="LineCharacters">单行行长上限。</param>
public readonly record struct ReadLimits(int Characters, int LineCharacters)
{
    /// <summary>正文总字符上限的缺省值。</summary>
    public const int DefaultCharacters = 10000;

    /// <summary>正文总字符上限允许的最大值。</summary>
    public const int MaximumCharacters = 50000;

    /// <summary>单行行长上限的缺省值。</summary>
    public const int DefaultLineCharacters = 300;

    /// <summary>从工具实参解析 maxChars 与 maxLineChars。缺省取默认值，非数字、越界或非正数时拒绝。</summary>
    public static ErrorOr<ReadLimits> Parse(ToolArguments arguments)
    {
        ErrorOr<int> characters = ParseNumber(
            "maxChars",
            arguments.Text(new ToolName("maxChars")),
            DefaultCharacters);
        if (characters.IsError)
        {
            return characters.ErrorsOrEmptyList;
        }

        if (characters.Value is < 1 or > MaximumCharacters)
        {
            return ToolErrors.Argument(
                $"参数 maxChars 要在 1 到 {MaximumCharacters} 之间，收到 {characters.Value}。");
        }

        ErrorOr<int> lineCharacters = ParseNumber(
            "maxLineChars",
            arguments.Text(new ToolName("maxLineChars")),
            DefaultLineCharacters);
        if (lineCharacters.IsError)
        {
            return lineCharacters.ErrorsOrEmptyList;
        }

        if (lineCharacters.Value < 1)
        {
            return ToolErrors.Argument($"参数 maxLineChars 必须大于 0，收到 {lineCharacters.Value}。");
        }

        return new ReadLimits(characters.Value, lineCharacters.Value);
    }

    /// <summary>整数实参。缺省或空白用默认值，其余按整数解析；解析失败给出带参数名与实参的详细错误。</summary>
    private static ErrorOr<int> ParseNumber(string parameter, string? text, int @default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return @default;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
            ? number
            : ToolErrors.Argument($"参数 {parameter} 要写成数字，收到 {text}。");
    }
}