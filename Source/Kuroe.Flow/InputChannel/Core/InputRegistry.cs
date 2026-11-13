using System.Runtime.CompilerServices;
using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;

namespace Kuroe.Flow.InputChannel;

/// <summary>外部输入协议的默认承载：按任务记下此刻接收外部输入的节点、提问与交付入口。
/// 节点侧声明与宿主侧读取共用这一份登记，桶内读写都在任务 Gate 下，与运行期状态的其他读写同一把锁。
/// 跨任务的结构安全与条目生命周期由弱键表承担，条目随任务对象回收。
/// 进程内只应有一个实例，装配处须把同一实例交给节点类型实例与宿主。</summary>
public sealed class InputRegistry : IInputChannel, IInputDelivery
{
    private readonly ConditionalWeakTable<INodeContext, Dictionary<NodePath, Acceptance>> _nodes = [];

    /// <summary>声明本节点此刻接收外部输入。</summary>
    public void Accept(INodeContext task, NodePath node, string? question, InputReceiver receive) =>
        Nodes(task)[node] = new Acceptance(question, receive);

    /// <summary>声明本节点不再接收外部输入。</summary>
    public void Decline(INodeContext task, NodePath node)
    {
        if (_nodes.TryGetValue(task, out Dictionary<NodePath, Acceptance>? nodes))
        {
            nodes.Remove(node);
        }
    }

    /// <summary>任务里此刻接收外部输入的声明，无序集合。</summary>
    public IReadOnlyList<InputRequest> Accepting(INodeContext task) =>
        _nodes.TryGetValue(task, out Dictionary<NodePath, Acceptance>? nodes)
            ? [.. nodes.Select(entry => new InputRequest(entry.Key, entry.Value.Question))]
            : [];

    /// <summary>交付一次外部输入并撤销该节点的声明。节点由接收集合给出，交付未登记的节点说明调用方没按接收集合来。</summary>
    public void Deliver(INodeContext task, NodePath node, string input)
    {
        if (!_nodes.TryGetValue(task, out Dictionary<NodePath, Acceptance>? nodes)
            || !nodes.Remove(node, out Acceptance acceptance))
        {
            throw new InvalidOperationException($"节点「{node}」未声明接收外部输入。");
        }

        acceptance.Receive(input);
    }

    /// <summary>某任务下此刻接收外部输入的节点表，首次声明时建出。</summary>
    private Dictionary<NodePath, Acceptance> Nodes(INodeContext task) =>
        _nodes.GetValue(task, _ => []);

    /// <summary>一条接收声明：等待时的提问与交付入口。</summary>
    private readonly record struct Acceptance(string? Question, InputReceiver Receive);
}