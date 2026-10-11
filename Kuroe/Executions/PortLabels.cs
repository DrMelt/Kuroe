using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Executions;

/// <summary>输出端口的展示名。</summary>
public static class PortLabels
{
    /// <summary>输出端口的展示：Data 端口只写名字，其余标出种类，说明存在时写在末尾。</summary>
    public static string Label(this Port port)
    {
        string name = port.Kind == PortKind.Data ? port.Name.Value : $"{port.Name.Value}·{port.Kind}";

        return port.Description is { Length: > 0 } description ? $"{name}：{description}" : name;
    }
}
