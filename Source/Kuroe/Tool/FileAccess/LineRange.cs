using System.Globalization;
using ErrorOr;

namespace Kuroe.Tool.FileAccess;

/// <summary>工具实参里的行号范围，行号从 1 起。缺省值由调用方给定，取 0 表示该行号必填。</summary>
public readonly record struct LineRange(int From, int To)
{
    /// <summary>从工具实参解析 from 与 to，缺省或空白取给定量；非数字、小于 1 或 to 小于 from 时给出拒绝错误。</summary>
    public static ErrorOr<LineRange> Parse(ToolArguments arguments, int defaultFrom, int defaultTo)
    {
        ErrorOr<int> from = ParseNumber("from", arguments.Text(new ToolName("from")), defaultFrom);
        if (from.IsError)
        {
            return from.ErrorsOrEmptyList;
        }

        if (from.Value < 1)
        {
            return ToolErrors.Argument("起始行号 from 从 1 起。");
        }

        ErrorOr<int> to = ParseNumber("to", arguments.Text(new ToolName("to")), defaultTo);
        if (to.IsError)
        {
            return to.ErrorsOrEmptyList;
        }

        if (to.Value < 1)
        {
            return ToolErrors.Argument("结束行号 to 从 1 起。");
        }

        if (to.Value < from.Value)
        {
            return ToolErrors.Argument("结束行号 to 不能小于起始行号 from。");
        }

        return new LineRange(from.Value, to.Value);
    }

    /// <summary>行号从文本取数。缺省或空白用默认值，其余按整数解析；解析失败给出带参数名与实参的详细错误。</summary>
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
