namespace Kuroe.Agent;

/// <summary>上下文中的一条已有内容及其出处。</summary>
public sealed record ContextMessage(MessageRole Role, string Text, ContextSource Source);
