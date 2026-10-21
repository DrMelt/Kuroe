using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>流程编译出的执行视图的只读面：节点以从根到本节点的路径标识，开始是固定来源，
/// 执行节点与容器在同一棵树上，依赖以显式边表达。节点表、树遍历与输入侧方案由实现给出，读取方只依赖这里。</summary>
public interface INodeGraph
{
    /// <summary>全部节点：开始、执行节点与容器，按先根序。</summary>
    IReadOnlyList<GraphNode> Nodes { get; }

    /// <summary>流程的唯一来源节点。</summary>
    StartNode Start { get; }

    /// <summary>执行节点，按先根序。</summary>
    IReadOnlyList<ExecutableNode> ExecutableNodes { get; }

    /// <summary>容器节点，按先根序。</summary>
    IReadOnlyList<ContainerNode> Containers { get; }

    /// <summary>依赖边。</summary>
    IReadOnlyList<FlowEdge> Edges { get; }

    /// <summary>节点数，执行节点与容器一起算。</summary>
    int Count { get; }

    /// <summary>执行节点数。</summary>
    int TotalExecutables { get; }

    /// <summary>按路径取一个节点，不存在时抛出异常。</summary>
    GraphNode this[NodePath path] { get; }

    /// <summary>直接子节点：容器给成员执行节点与子容器，路径为空给根级节点，按先根序。</summary>
    IReadOnlyList<GraphNode> ChildrenOf(NodePath? container);

    /// <summary>节点直接所属的容器，根级节点为空。</summary>
    ContainerNode? ContainerOf(NodePath path);

    /// <summary>目标的入边，按编译顺序。</summary>
    IReadOnlyList<FlowEdge> Incoming(NodePath target);

    /// <summary>来源的出边，按编译顺序。</summary>
    IReadOnlyList<FlowEdge> Outgoing(NodePath source);

    /// <summary>容器及它的全部子容器里的执行节点路径，递归展开。容器给全部成员，执行节点给自身。</summary>
    IReadOnlyList<NodePath> ExecutablesIn(NodePath source);

    /// <summary>节点的输入侧方案，未参与就绪判定的节点为空。</summary>
    NodeInputPlan? PlanOf(NodePath path);

    /// <summary>节点类型的形状，开始为空。</summary>
    NodeTypeShape? ShapeOf(NodePath path);

    /// <summary>来源节点某端口声明的类型，容器与未声明按 Text。</summary>
    PortType TypeOf(NodePath node, PortName port);
}
