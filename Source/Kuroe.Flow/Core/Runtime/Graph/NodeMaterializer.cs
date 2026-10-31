using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.Runtime.Graph;

/// <summary>运行期节点的物化：按图定义建立节点对象，按路径逐段下钻，未命中时按需建出缺失的段。
/// 容器的直接子节点登记在容器上，根级节点由这里持有。取消后迟实例化的节点创建即取消。</summary>
internal sealed class NodeMaterializer(
    INodeGraph graph,
    IPortTexts startPorts,
    INodeContext context)
{
    /// <summary>已实例化的根级节点：开始与流程根节点。容器的直接子节点由容器自己持有。</summary>
    private readonly List<NodeRuntime> _roots = [];

    private bool _canceled;

    /// <summary>已实例化的节点，先根序。</summary>
    internal IEnumerable<NodeRuntime> Materialized => _roots.SelectMany(Subtree);

    /// <summary>按路径取节点，未实例化时建出缺失的段。要求持有任务 Gate。</summary>
    internal NodeRuntime Get(NodePath path) => Walk(path, materialize: true)!;

    /// <summary>已实例化的节点，未实例化时为空。不创建节点。</summary>
    internal NodeRuntime? Find(NodePath path) => Walk(path, materialize: false);

    /// <summary>已实例化的执行节点，按执行节点表顺序。不创建节点。</summary>
    internal IEnumerable<RuntimeExecutable> Executables() =>
        graph.ExecutableNodes.Select(executable => Find(executable.Path)).OfType<RuntimeExecutable>();

    /// <summary>任务取消：已实例化的节点全部进入取消态，迟实例化的节点创建即取消。</summary>
    internal void CancelAll()
    {
        _canceled = true;
        foreach (NodeRuntime node in Materialized)
        {
            node.Cancel();
        }
    }

    /// <summary>沿路径逐段下钻：命中已实例化的节点即取它，materialize 为真时建出缺失的段。
    /// 中间段必须是容器，子节点登记在所在容器上。</summary>
    private NodeRuntime? Walk(NodePath path, bool materialize)
    {
        IReadOnlyList<NodeRuntime> level = _roots;
        RuntimeContainer? owner = null;
        NodeRuntime? current = null;
        for (int depth = 1; depth <= path.Depth; depth++)
        {
            NodePath prefix = new([.. path.Levels.Take(depth)]);
            current = level.FirstOrDefault(node => node.Path.Label == prefix.Label);
            if (current is null)
            {
                if (!materialize)
                {
                    return null;
                }

                current = Create(prefix);
                if (owner is null)
                {
                    _roots.Add(current);
                }
                else
                {
                    owner.Add(current);
                }

                if (_canceled)
                {
                    current.Cancel();
                }
            }

            if (depth == path.Depth)
            {
                return current;
            }

            if (current is not RuntimeContainer container)
            {
                throw new ArgumentException($"路径 {path} 的中间段不是容器，无法下钻。");
            }

            owner = container;
            level = container.Children;
        }

        return current;
    }

    /// <summary>按形状建立节点对象：开始给开始输入，容器与派发执行的节点由引擎建立，其余由类型自建。</summary>
    private NodeRuntime Create(NodePath path) =>
        graph[path] switch
        {
            StartNode start => new RuntimeStart(start, startPorts),
            ExecutableNode executable when executable.Type.Shape.DispatchesExecutions =>
                new RuntimeExecutable(executable),
            ContainerNode container when container.Type.Shape.Organizes =>
                new RuntimeContainer(container, context),
            ContainerNode container => Factory(container.Type, container.Path).Create(container, context),
            ExecutableNode executable => Factory(executable.Type, executable.Path).Create(executable, context),
            _ => throw new InvalidOperationException($"节点 {path} 没有类型，无法建立运行期对象。"),
        };


    /// <summary>取节点类型的运行期工厂，类型没有提供时给出错误。</summary>
    private static INodeRuntimeFactory Factory(INodeType type, NodePath path) =>
        type as INodeRuntimeFactory
        ?? throw new InvalidOperationException($"节点 {path} 的类型 {type.Kind.Value} 没有提供运行期对象。");

    private static IEnumerable<NodeRuntime> Subtree(NodeRuntime node) =>
        node is RuntimeContainer container ? [node, .. container.Children.SelectMany(Subtree)] : [node];
}