namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>收到一段执行产出内容。</summary>
/// <param name="content">本次增量的内容与种类。</param>
public delegate void ProcessContentHandler(ProcessContent content);

/// <summary>收到一条需要即时呈现的过程记录。</summary>
/// <param name="entry">写入的过程记录。</param>
public delegate void JournalEntryHandler(JournalEntry entry);

/// <summary>收到当前活动文本。</summary>
/// <param name="text">当前活动，没有活动时为空文本。</param>
public delegate void ProgressTextHandler(string text);

/// <summary>一个任务或一次执行的过程观察者：为空的部分不观察。</summary>
/// <param name="Content">产出内容到达时调用。</param>
/// <param name="Entry">过程记录写入时调用。</param>
public sealed record ProcessObserver(ProcessContentHandler? Content, JournalEntryHandler? Entry);