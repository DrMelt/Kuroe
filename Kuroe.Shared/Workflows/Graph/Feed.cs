using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

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

/// <summary>边取用方式的推导：开始与容器来源只给单份，执行节点按来源与目标模式组合，
/// 声明为拆分的来源端口给目标实例空间。</summary>
public static class FeedRules
{
    /// <summary>逐条对齐、等全部实例、提供拆分空间、取单份产出。拆分支票端口只对按条目目标有效。</summary>
    public static (EdgeKind Kind, FeedScope Scope, FeedRecurrence Recurrence) Of(
        GraphNode source,
        NodeMode targetMode,
        PortName port) => source switch
        {
            StartNode => (EdgeKind.Value, FeedScope.Whole, FeedRecurrence.Repeat),
            ContainerNode => (EdgeKind.Value, FeedScope.Whole, FeedRecurrence.Repeat),
            ExecutableNode executable => (executable.Execution.Mode == NodeMode.PerItem, targetMode, TypeOf(executable, port)) switch
            {
                (true, NodeMode.PerItem, _) => (EdgeKind.Value, FeedScope.Item, FeedRecurrence.Repeat),
                (true, _, _) => (EdgeKind.Value, FeedScope.AllInstances, FeedRecurrence.Once),
                (false, NodeMode.PerItem, PortType.Items) => (EdgeKind.Split, FeedScope.Whole, FeedRecurrence.Repeat),
                _ => (EdgeKind.Value, FeedScope.Whole, FeedRecurrence.Repeat),
            },
            // 图上的来源只有开始、容器与执行节点三类，此分支不可达
            _ => throw new ArgumentException($"不支持的来源节点类型：{source.GetType().Name}"),
        };

    /// <summary>来源端口声明的类型，未声明时按 Data。</summary>
    public static PortType TypeOf(ExecutableNode source, PortName port) =>
        source.FindPort(port)?.Type ?? PortType.Data;
}
