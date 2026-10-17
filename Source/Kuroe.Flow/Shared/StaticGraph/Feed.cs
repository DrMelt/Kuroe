namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>一条依赖边在目标输入侧的角色：从来源取值，或提供目标的实例空间来源。</summary>
public enum EdgeKind
{
    /// <summary>从来源取值的绑定边，参与所属启动组的齐备判定。</summary>
    Value,

    /// <summary>提供目标实例空间来源的空间边，目标按该来源的拆分展开实例，只参与所属组的齐备判定。</summary>
    Split,
}

/// <summary>取值边从来源取哪一域的值。</summary>
public enum FeedScope
{
    /// <summary>取来源最近发布的一份产出。</summary>
    Whole,

    /// <summary>取来源全部实例的产出，等来源全部实例齐备。</summary>
    AllInstances,

    /// <summary>取来源同序号实例的产出，目标实例承担哪个条目才需要哪个条目的产出。</summary>
    Item,
}

/// <summary>取值边的消费次数：来源每次发布新版本都消费，或整套只消费一次。</summary>
public enum FeedRecurrence
{
    /// <summary>来源每有新版本即重新消费。</summary>
    Repeat,

    /// <summary>整套产出只消费一次，来源再发布也不重复消费。</summary>
    Once,
}
