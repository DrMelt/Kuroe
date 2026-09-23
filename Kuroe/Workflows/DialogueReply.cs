namespace Kuroe.Workflows;

/// <summary>一轮前台对话的产出：完整回复，以及本轮输入与输出是否未计入上下文。</summary>
public sealed record DialogueReply(string Text, bool Discarded);