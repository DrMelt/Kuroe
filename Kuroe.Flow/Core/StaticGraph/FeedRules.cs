using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.StaticGraph;

/// <summary>边取用方式的推导：开始与容器来源只给单份，执行节点按来源与目标模式组合，
/// 声明为拆分的来源端口给目标实例空间。</summary>
public static class FeedRules
{
    /// <summary>逐条对齐、等全部实例、提供拆分空间、取单份产出。WorkItems 端口只对按条目目标有效。</summary>
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
                (false, NodeMode.PerItem, PortType.WorkItems) => (EdgeKind.Split, FeedScope.Whole, FeedRecurrence.Repeat),
                _ => (EdgeKind.Value, FeedScope.Whole, FeedRecurrence.Repeat),
            },
            // 图上的来源只有开始、容器与执行节点三类，此分支不可达
            _ => throw new ArgumentException($"不支持的来源节点形态：{source.GetType().Name}"),
        };

    /// <summary>来源端口声明的类型，未声明时按 Text。</summary>
    public static PortType TypeOf(ExecutableNode source, PortName port) =>
        source.FindPort(port)?.Type ?? PortType.Text;
}
