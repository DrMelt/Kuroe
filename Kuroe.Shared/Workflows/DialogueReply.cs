namespace Kuroe.Shared.Workflows;

/// <summary>对话一轮的产出：完整回复。轮次历史由对话任务自身累积。</summary>
public sealed record DialogueReply(string Text);
