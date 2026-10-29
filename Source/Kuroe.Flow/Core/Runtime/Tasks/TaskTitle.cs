using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>任务标题的推导与截断：未给出标题时取第一个开始输入的首行，标题过长时截断。</summary>
internal static class TaskTitle
{
    /// <summary>标题的字符上限。</summary>
    private const int Limit = 40;

    /// <summary>按提交输入、流程名与可选标题给出任务标题。</summary>
    public static string Compose(StartInputs inputs, FlowName flow, string? title) =>
        Shorten(string.IsNullOrWhiteSpace(title) ? FirstInput(inputs, flow) : title);

    /// <summary>取文本首行作标题，过长时截断。</summary>
    public static string Shorten(string text)
    {
        string first = text.Trim().Split('\n')[0].Trim();

        return first.Length <= Limit ? first : string.Concat(first.AsSpan(0, Limit), "…");
    }

    /// <summary>第一个开始输入的文本，没有输入时取流程名。</summary>
    private static string FirstInput(StartInputs inputs, FlowName flow) =>
        inputs.Values.Count > 0 ? inputs.Values[0].Text : flow.Value;
}