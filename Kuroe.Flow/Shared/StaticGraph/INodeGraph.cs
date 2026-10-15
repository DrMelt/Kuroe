namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>流程编译出的执行视图的只读面：节点按先根序统一编号，开始在最前，执行节点与容器在一张表里，
/// 依赖以显式边表达。编号、索引与输入侧方案由实现给出，读取方只依赖这里。</summary>
public interface INodeGraph
{
    /// <summary>全部节点：开始、执行节点与容器，按先根序统一编号。</summary>
    IReadOnlyList<GraphNode> Nodes { get; }

    /// <summary>流程的唯一来源节点，序号 0。</summary>
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

    /// <summary>取一个节点。</summary>
    GraphNode this[int index] { get; }

    /// <summary>按名定位节点，不存在时为空。</summary>
    int? IndexOf(NodeName name);

    /// <summary>节点直接所属的容器，根级节点为空。</summary>
    ContainerNode? ContainerOf(int nodeIndex);

    /// <summary>目标的入边，按编译顺序。</summary>
    IReadOnlyList<FlowEdge> Incoming(int target);

    /// <summary>来源的出边，按编译顺序。</summary>
    IReadOnlyList<FlowEdge> Outgoing(int source);

    /// <summary>容器及它的全部子容器里的执行节点，递归展开。</summary>
    IReadOnlyList<int> ExecutablesIn(int containerIndex);

    /// <summary>节点的输入侧方案，未参与就绪判定的节点为空。</summary>
    NodeInputPlan? PlanOf(int index);

    /// <summary>节点类型的形状，类型未注册时为空。</summary>
    NodeTypeShape? ShapeOf(int index);

    /// <summary>来源节点某端口声明的类型，容器与未声明按 Text。</summary>
    PortType TypeOf(int nodeIndex, PortName port);

    /// <summary>来源节点提供实例空间的拆分标识，没有时为空。</summary>
    int? ItemSpace(int executableIndex);
}
