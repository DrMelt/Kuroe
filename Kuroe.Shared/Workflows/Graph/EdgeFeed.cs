using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>一条边上的消费方式：目标执行节点期望从来源取哪种产出，来源是执行节点时按
/// 来源与目标模式在编译期推导。端口只指明取哪个端口，与消费方式正交。</summary>
public enum EdgeFeed
{
    /// <summary>取来源最近发布的一份产出。第 Port 端口取值，PerItem 来源给全部实例拼接。</summary>
    Single,

    /// <summary>目标按来源交回的拆分支票展开实例。规划来源配 PerItem 目标，端口是保留的拆分端口。</summary>
    Items,

    /// <summary>等来源的全部条目实例产出齐备后取该端口全部实例产出。PerItem 来源配 Single 目标。</summary>
    AllInstances,

    /// <summary>按条目序号对齐来源的实例产出。PerItem 来源配 PerItem 目标。</summary>
    Aligned,
}

/// <summary>边消费方式的推导规则：开始与容器来源只给单份，执行节点按来源与目标模式组合，
/// 声明为拆分的来源端口给条目展开。</summary>
public static class EdgeFeedRules
{
    /// <summary>逐条对齐、等全部实例、取拆分、取单份产出。拆分支票端口只对按条目目标有效。</summary>
    public static EdgeFeed Of(GraphNode source, NodeMode targetMode, PortName port) => source switch
    {
        StartNode => EdgeFeed.Single,
        ContainerNode => EdgeFeed.Single,
        ExecutableNode executable => (executable.Execution.Mode == NodeMode.PerItem, targetMode, KindOf(executable, port)) switch
        {
            (true, NodeMode.PerItem, _) => EdgeFeed.Aligned,
            (true, _, _) => EdgeFeed.AllInstances,
            (false, NodeMode.PerItem, PortKind.Items) => EdgeFeed.Items,
            _ => EdgeFeed.Single,
        },
        _ => throw new ArgumentException($"不支持的来源节点类型：{source.GetType().Name}"),
    };

    /// <summary>来源端口声明的种类，未声明时按 Data。</summary>
    public static PortKind KindOf(ExecutableNode source, PortName port) =>
        source.FindPort(port)?.Kind ?? PortKind.Data;
}
