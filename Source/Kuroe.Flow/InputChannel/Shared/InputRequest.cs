using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.InputChannel.Shared;

/// <summary>一次外部输入接收声明：接收输入的节点与等待时的提问。</summary>
/// <param name="Node">声明接收外部输入的节点。</param>
/// <param name="Question">等待时给外部的提问，未声明时为空。</param>
public readonly record struct InputRequest(NodePath Node, string? Question);