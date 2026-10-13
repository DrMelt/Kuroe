using Kuroe.Flows.Shared.StaticGraph;

namespace Kuroe.Executions;

/// <summary>节点类型的展示名。</summary>
public static class NodeKindLabels
{
    /// <summary>节点类型的展示名。</summary>
    public static string Label(this NodeKind kind) => kind switch
    {
        NodeKind.Container => "容器",
        NodeKind.Executable => "执行",
        NodeKind.Input => "输入",
        _ => kind.ToString(),
    };
}
