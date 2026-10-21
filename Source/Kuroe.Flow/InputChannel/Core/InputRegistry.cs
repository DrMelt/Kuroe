using System.Runtime.CompilerServices;
using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.InputChannel;

/// <summary>外部输入协议的默认承载：按任务记下此刻接收外部输入的节点与交付入口。
/// 节点侧声明与宿主侧读取共用这一份登记，桶内读写都在任务 Gate 下，与运行期状态的其他读写同一把锁。
/// 跨任务的结构安全与条目生命周期由弱键表承担，条目随任务对象回收。
/// 进程内只应有一个实例，装配处须把同一实例交给节点类型实例与宿主。</summary>
public sealed class InputRegistry : IInputChannel, IInputDelivery
{
    private readonly ConditionalWeakTable<INodeContext, Dictionary<NodePath, Action<string>>> _nodes = [];

    /// <summary>声明本节点此刻接收外部输入。</summary>
    public void Accept(INodeContext task, NodePath node, Action<string> receive) =>
        Nodes(task)[node] = receive;

    /// <summary>声明本节点不再接收外部输入。</summary>
    public void Decline(INodeContext task, NodePath node)
    {
        if (_nodes.TryGetValue(task, out Dictionary<NodePath, Action<string>>? nodes))
        {
            nodes.Remove(node);
        }
    }

    /// <summary>任务里此刻接收外部输入的节点，无序集合。</summary>
    public IReadOnlyList<NodePath> Accepting(INodeContext task) =>
        _nodes.TryGetValue(task, out Dictionary<NodePath, Action<string>>? nodes)
            ? [.. nodes.Keys]
            : [];

    /// <summary>交付一次外部输入并撤销该节点的声明。节点由接收集合给出，交付未登记的节点说明调用方没按接收集合来。</summary>
    public void Deliver(INodeContext task, NodePath node, string input)
    {
        if (!_nodes.TryGetValue(task, out Dictionary<NodePath, Action<string>>? nodes)
            || !nodes.Remove(node, out Action<string>? receive))
        {
            throw new InvalidOperationException($"节点「{node}」未声明接收外部输入。");
        }

        receive(input);
    }

    /// <summary>某任务下此刻接收外部输入的节点表，首次声明时建出。</summary>
    private Dictionary<NodePath, Action<string>> Nodes(INodeContext task) =>
        _nodes.GetValue(task, _ => []);
}