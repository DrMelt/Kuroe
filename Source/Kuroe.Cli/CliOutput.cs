using ErrorOr;
using Kuroe.Flow.Shared.Execution;

namespace Kuroe.Cli;

/// <summary>stdout 与 stderr 的写出口。stdout 承载模型文本与结果，stderr 承载进度、工具调用与错误，全部写入串行。</summary>
internal sealed class CliOutput(TextWriter output, TextWriter error)
{
    private readonly Lock _gate = new();
    private bool _lineOpen;

    /// <summary>写出文本，不补换行，用于流式增量。</summary>
    public void Text(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            output.Write(text);
            output.Flush();
            _lineOpen = !text.EndsWith('\n');
        }
    }

    /// <summary>写出完整一行。</summary>
    public void Line(string text = "")
    {
        lock (_gate)
        {
            output.WriteLine(text);
            output.Flush();
            _lineOpen = false;
        }
    }

    /// <summary>结束当前未闭合的行。</summary>
    public void EnsureLine()
    {
        lock (_gate)
        {
            if (!_lineOpen)
            {
                return;
            }

            output.WriteLine();
            output.Flush();
            _lineOpen = false;
        }
    }

    /// <summary>写一行提示到 stderr。</summary>
    public void Hint(string text) => ErrorLine(text);

    /// <summary>写一行错误到 stderr。</summary>
    public void ErrorLine(string text)
    {
        lock (_gate)
        {
            error.WriteLine(text);
            error.Flush();
        }
    }

    /// <summary>一次工具调用及其结果写 stderr，避免与 stdout 上的模型文本交错。</summary>
    public void ToolCall(ToolCallRecord record)
    {
        lock (_gate)
        {
            error.WriteLine(record.Arguments.Length == 0
                ? $"工具 {record.Name}"
                : $"工具 {record.Name} {record.Arguments}");
            error.WriteLine(record.Failed ? $"失败 {record.Outcome}" : $"结果 {record.Outcome}");
            error.Flush();
        }
    }

    /// <summary>按错误码与说明输出全部错误。</summary>
    public void Report(IEnumerable<Error> errors)
    {
        foreach (Error failure in errors)
        {
            ErrorLine($"错误：{failure.Code}：{failure.Description}");
        }
    }
}