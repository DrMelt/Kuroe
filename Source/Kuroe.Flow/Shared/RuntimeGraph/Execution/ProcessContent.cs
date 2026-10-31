namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>流式过程内容的种类。</summary>
public enum ProcessContentKind
{
    /// <summary>面向用户的回复。</summary>
    Reply,

    /// <summary>模型在回复前给出的思考。</summary>
    Reasoning,
}

/// <summary>一次执行的一段流式内容。它只用于过程通知，不做持久化；过程记录仍由 <see cref="JournalEntry"/> 承载。</summary>
/// <param name="Kind">内容种类。</param>
/// <param name="Delta">本次到达的文本增量。</param>
public readonly record struct ProcessContent(ProcessContentKind Kind, string Delta);
