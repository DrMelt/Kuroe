namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>来源端口的一段产出与它的出处。文本是原样产出，措辞由消费方决定。</summary>
public sealed record ContextValue(ContextSource Origin, string Text);