
namespace Kuroe.Execution.Shared;

/// <summary>执行产出内容的种类。</summary>
public enum ProcessContentKind
{
    /// <summary>面向用户的回复。</summary>
    Reply,

    /// <summary>模型在回复前给出的思考。</summary>
    Reasoning,
}

/// <summary>一次执行的一段产出内容。它只用于过程通知，不做持久化；过程记录仍由 <see cref="JournalEntry"/> 承载。</summary>
/// <param name="Kind">内容种类。</param>
/// <param name="Delta">本次到达的文本内容。</param>
public readonly record struct ProcessContent(ProcessContentKind Kind, string Delta);
