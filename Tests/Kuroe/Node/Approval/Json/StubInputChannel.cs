using Kuroe.Flow.InputChannel;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Node.Approval.Json.Tests;

/// <summary>测试用的外部输入协议替身：记录声明与撤销，供用例断言。</summary>
internal sealed class StubInputChannel : IInputChannel
{
    /// <summary>收到的声明，按声明顺序。</summary>
    public List<(NodePath Node, Action<string> Receive)> Declared { get; } = [];

    /// <summary>收到的撤销，按撤销顺序。</summary>
    public List<NodePath> Declined { get; } = [];

    /// <summary>声明本节点此刻接收外部输入。</summary>
    public void Accept(INodeContext task, NodePath node, Action<string> receive) =>
        Declared.Add((node, receive));

    /// <summary>声明本节点不再接收外部输入。</summary>
    public void Decline(INodeContext task, NodePath node) =>
        Declined.Add(node);
}