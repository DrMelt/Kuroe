using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>流程编译出的执行视图的只读面：节点以从根到本节点的路径标识，开始是固定来源，
/// 执行节点与容器在同一棵树上，依赖由 From 绑定、静态输入的运行期常量依赖与容器触发输入共同表达。节点表、树遍历与输入侧方案由实现给出，读取方只依赖这里。</summary>
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

    /// <summary>驱动绑定，按编译顺序。</summary>
    IReadOnlyList<InputBinding> Bindings { get; }

    /// <summary>静态启动条件：静态输入绑定到运行期常量产出。</summary>
    IReadOnlyList<InputBinding> StaticBindings { get; }

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

    /// <summary>目标的驱动绑定，按编译顺序。</summary>
    IReadOnlyList<InputBinding> Incoming(NodePath target);

    /// <summary>来源的驱动绑定，按编译顺序。</summary>
    IReadOnlyList<InputBinding> Outgoing(NodePath source);

    /// <summary>来源发布后需要重新评估的目标，包含静态依赖节点与使用该来源的容器触发输入。</summary>
    IReadOnlyList<NodePath> Dependents(NodePath source);

    /// <summary>执行节点某静态输入的结构解析结果，未声明或未解析时为空。</summary>
    StaticResolution? StaticInputResolution(NodePath node, PortName port);

    /// <summary>节点某常量输出的结构解析结果，未声明或未解析时为空。</summary>
    StaticResolution? ConstantResolution(NodePath node, PortName port);

    /// <summary>容器及它的全部子容器里的执行节点路径，递归展开。容器给全部成员，执行节点给自身。</summary>
    IReadOnlyList<NodePath> ExecutablesIn(NodePath source);

    /// <summary>节点的输入侧方案，开始为空。</summary>
    NodeInputPlan? PlanOf(NodePath path);

    /// <summary>节点类型的形状，开始为空。</summary>
    NodeTypeShape? ShapeOf(NodePath path);

    /// <summary>来源节点某输出端口声明的类型，未声明按 Text。</summary>
    PortType TypeOf(NodePath node, PortName port);
}
