using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.InputChannel;

/// <summary>外部输入协议的宿主侧交付面：读出任务此刻接收外部输入的节点，并交付一次外部输入。
/// 节点侧只用 <see cref="IInputChannel"/> 声明接收，交付入口由节点在声明时给出，这里只按登记节点调节点给的入口。</summary>
public interface IInputDelivery
{
    /// <summary>任务里此刻接收外部输入的节点，无序集合。</summary>
    IReadOnlyList<NodePath> Accepting(INodeContext task);

    /// <summary>交付一次外部输入并撤销该节点的声明。交付未登记的节点说明调用方没按接收集合来，是调用方不变量违背。</summary>
    void Deliver(INodeContext task, NodePath node, string input);
}