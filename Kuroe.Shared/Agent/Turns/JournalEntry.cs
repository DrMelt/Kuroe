namespace Kuroe.Shared.Agent.Turns;

/// <summary>过程记录中的一条，按发生顺序排列。</summary>
public abstract record JournalEntry(DateTimeOffset At);

/// <summary>发给模型的一句话或一条指令。</summary>
public sealed record PromptEntry(string Text) : JournalEntry(DateTimeOffset.UtcNow);

/// <summary>模型输出的一段文本。</summary>
public sealed record TextEntry(string Text) : JournalEntry(DateTimeOffset.UtcNow);

/// <summary>一次工具调用及其结果。</summary>
public sealed record ToolCallEntry(ToolCallRecord Call) : JournalEntry(DateTimeOffset.UtcNow);

/// <summary>一条失败说明。</summary>
public sealed record ErrorEntry(string Text) : JournalEntry(DateTimeOffset.UtcNow);

/// <summary>本轮输入与输出未计入上下文。</summary>
public sealed record DiscardedEntry() : JournalEntry(DateTimeOffset.UtcNow);
