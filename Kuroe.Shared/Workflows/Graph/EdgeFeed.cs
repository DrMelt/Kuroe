using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>一条边上的消费方式：目标执行节点期望从来源取哪种产出，来源是执行节点时按
/// 来源与目标模式在编译期推导，来源是容器时固定取整份齐备状态。</summary>
public enum EdgeFeed
{
    /// <summary>取来源最近发布的一整份产出。Plan 来源给拆分清单，其余执行节点给文本，容器来源给整容器齐备。</summary>
    Single,

    /// <summary>目标按来源交回的拆分条目展开实例。Plan 来源配 PerItem 目标。</summary>
    Items,

    /// <summary>等来源的全部条目实例产出齐备。PerItem 来源配 Single 目标。</summary>
    AllInstances,

    /// <summary>按条目序号对齐来源的实例产出。PerItem 来源配 PerItem 目标。</summary>
    Aligned,
}

/// <summary>边消费方式的推导规则：容器来源只给整份齐备，其余按来源与目标执行模式的组合。</summary>
public static class EdgeFeedRules
{
    /// <summary>逐条对齐、等全部实例、取拆分、取单份产出。</summary>
    public static EdgeFeed Of(GraphNode source, NodeMode targetMode) => source switch
    {
        ContainerNode => EdgeFeed.Single,
        ExecutableNode executable => targetMode switch
        {
            NodeMode.PerItem => executable.Mode == NodeMode.PerItem ? EdgeFeed.Aligned : EdgeFeed.Items,
            _ => executable.Mode == NodeMode.PerItem ? EdgeFeed.AllInstances : EdgeFeed.Single,
        },
        _ => throw new ArgumentException($"不支持的来源节点类型：{source.GetType().Name}"),
    };
}
