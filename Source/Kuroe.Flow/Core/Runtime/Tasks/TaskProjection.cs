using Kuroe.Flow.Runtime.Graph;
using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>任务快照的只读投影：按已实例化节点读出执行节点与容器的一刻状态。
/// 投影不实例化节点，未实例化的节点按默认待办报告。</summary>
internal sealed class TaskProjection(INodeGraph graph, NodeMaterializer materializer)
{
    /// <summary>节点在快照里的一刻状态：节点自己给出，未实例化的节点按默认待办报告。</summary>
    public ExecutableStateSnapshot State(NodePath path) => materializer.Find(path)?.Snapshot()
        ?? new ExecutableStateSnapshot(path, NodeState.Pending);

    /// <summary>各容器在快照里的一刻状态：内容齐备情况与成员活动。</summary>
    public IReadOnlyList<ContainerSnapshot> Containers()
    {
        List<ContainerSnapshot> snapshots = [];
        foreach (ContainerNode container in graph.Containers)
        {
            snapshots.Add(materializer.Find(container.Path) is RuntimeContainer runtime
                ? Of(runtime)
                : new ContainerSnapshot(container.Path, ActivityState(container), ChildPaths(container)));
        }

        return snapshots;
    }

    /// <summary>已实例化容器的一刻状态。成员状态只在已实例化成员上读，未实例化成员视为没有活动。</summary>
    private ContainerSnapshot Of(RuntimeContainer container)
    {
        NodeState state;
        if (container.Canceled)
        {
            state = NodeState.Canceled;
        }
        else if (container.Released)
        {
            state = NodeState.Done;
        }
        else
        {
            state = ActivityState(container.Container);
        }

        return new ContainerSnapshot(container.Path, state, ChildPaths(container.Container));
    }

    /// <summary>容器的直接子节点路径，先根序。</summary>
    private static IReadOnlyList<NodePath> ChildPaths(ContainerNode container) =>
        [.. container.Children.Select(child => child.Path)];

    /// <summary>容器里已实例化成员的活动状态：有在跑或已产出即运行中，都没有即待办。</summary>
    private NodeState ActivityState(ContainerNode container) =>
        graph.ExecutablesIn(container.Path).Any(MemberActive) ? NodeState.Running : NodeState.Pending;

    /// <summary>成员执行节点是否有在跑的执行或已产出，未实例化的成员没有活动。</summary>
    private bool MemberActive(NodePath executable) =>
        materializer.Find(executable) is RuntimeExecutable node
            && (node.HasActive || node.PublishedExecution is not null);
}