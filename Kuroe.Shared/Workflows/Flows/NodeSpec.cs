namespace Kuroe.Shared.Workflows.Flows;

/// <summary>统一节点：定义、引用、执行节点与容器都落在同一表示上。
/// 有 <see cref="Use"/> 是引用库定义，否则是节点本身。有 <see cref="Nodes"/> 是容器，有 <see cref="Execution"/> 是执行节点。</summary>
public sealed record NodeSpec
{
    /// <summary>节点名：库定义名或流程/容器内的实例名。</summary>
    public required NodeName Name { get; init; }

    /// <summary>引用的节点库定义名，非空即引用形态。</summary>
    public NodeName? Use { get; init; }

    /// <summary>节点进展到待批点时是否停人等批准：执行节点产出后，容器成员产出齐备后。</summary>
    public NodeGate Gate { get; init; } = NodeGate.Auto;

    /// <summary>容器声明传入端口名，成员可用 @端口 引用。</summary>
    public IReadOnlyList<NodeName> Inputs { get; init; } = [];

    /// <summary>容器引用把端口绑定到当前作用域可达节点，绑定值可以是节点名或 @更外层端口。</summary>
    public IReadOnlyDictionary<NodeName, NodeName>? In { get; init; }

    /// <summary>上下文取自哪些更早节点的产出。执行节点定义上禁止声明，引用处注入。</summary>
    public IReadOnlyList<NodeName> From { get; init; } = [];

    /// <summary>有序子节点，非空即容器。容器自身不执行，成员产出齐备时作汇合点。</summary>
    public IReadOnlyList<NodeSpec>? Nodes { get; init; }

    /// <summary>模型选择引用：装配层执行节点写本流程的模型配置名，节点组容器成员写模型槽位名，由引用处的模型绑定解析。</summary>
    public ModelRef? Model { get; init; }

    /// <summary>引用节点组时的模型槽位绑定：槽位名到本流程模型配置名的映射，未绑定的槽位在展开时报错。</summary>
    public IReadOnlyDictionary<ModelRef, ModelRef>? Models { get; init; }

    /// <summary>执行配置，非空即执行节点。</summary>
    public ExecutableSpec? Execution { get; init; }
}