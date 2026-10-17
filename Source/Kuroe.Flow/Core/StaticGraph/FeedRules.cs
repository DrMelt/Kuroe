using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.StaticGraph;

/// <summary>边取用方式的推导：执行节点来源按自身展开模式与来源端口类型给出取值域，容器与开始来源只给单份。</summary>
public static class FeedRules
{
    /// <summary>逐条对齐、等全部实例、提供拆分空间、取单份产出。WorkItems 端口只对按条目目标有效。</summary>
    public static (EdgeKind Kind, FeedScope Scope, FeedRecurrence Recurrence) Of(
        NodeMode sourceMode,
        PortType sourcePortType,
        NodeMode targetMode) => (sourceMode == NodeMode.PerItem, targetMode, sourcePortType) switch
        {
            (true, NodeMode.PerItem, _) => (EdgeKind.Value, FeedScope.Item, FeedRecurrence.Repeat),
            (true, _, _) => (EdgeKind.Value, FeedScope.AllInstances, FeedRecurrence.Once),
            (false, NodeMode.PerItem, PortType.WorkItems) => (EdgeKind.Split, FeedScope.Whole, FeedRecurrence.Repeat),
            _ => (EdgeKind.Value, FeedScope.Whole, FeedRecurrence.Repeat),
        };

    /// <summary>来源端口声明的类型，未声明时按 Text。</summary>
    public static PortType TypeOf(ExecutableNode source, PortName port) =>
        source.FindPort(port)?.Type ?? PortType.Text;
}
