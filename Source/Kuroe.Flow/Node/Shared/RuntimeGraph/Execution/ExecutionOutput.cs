namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>一次执行的产出。内容产出给出文本，端口产出交回命名端口后只给空内容。</summary>
public sealed record ExecutionOutput(ExecutionOutputKind Kind, string Content = "")
{
    /// <summary>端口产出，运行器把取值交回命名端口。</summary>
    public static ExecutionOutput Ports { get; } = new(ExecutionOutputKind.Ports);

    /// <summary>内容产出，运行器交回一段文本。</summary>
    public static ExecutionOutput FromContent(string content) => new(ExecutionOutputKind.Content, content);
}