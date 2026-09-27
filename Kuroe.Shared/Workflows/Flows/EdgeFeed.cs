namespace Kuroe.Shared.Workflows.Flows;

/// <summary>一条边上的消费方式：目标节点期望从来源取哪种产出，编译期按来源与目标模式推导。</summary>
public enum EdgeFeed
{
    /// <summary>取来源最近发布的一整份产出。Plan 来源给拆分清单，其余执行节点给文本。</summary>
    Single,

    /// <summary>目标按来源交回的拆分条目展开实例。Plan 来源配 PerItem 目标。</summary>
    Items,

    /// <summary>等来源的全部条目实例产出齐备。PerItem 来源配 Single 目标。</summary>
    AllInstances,

    /// <summary>按条目序号对齐来源的实例产出。PerItem 来源配 PerItem 目标。</summary>
    Aligned,
}

/// <summary>边消费方式按来源与目标执行节点展开模式的推导规则。</summary>
public static class EdgeFeedRules
{
    /// <summary>逐条对齐、等全部实例、取拆分、取单份产出。</summary>
    public static EdgeFeed Of(NodeMode sourceMode, NodeMode targetMode) => targetMode switch
    {
        NodeMode.PerItem => sourceMode == NodeMode.PerItem ? EdgeFeed.Aligned : EdgeFeed.Items,
        _ => sourceMode == NodeMode.PerItem ? EdgeFeed.AllInstances : EdgeFeed.Single,
    };
}