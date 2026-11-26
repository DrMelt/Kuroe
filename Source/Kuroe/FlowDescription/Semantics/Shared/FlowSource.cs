using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.FlowDescription.Semantics.Shared;

/// <summary>来源终点：输入或输出端口最终取值的种类与载荷。</summary>
public readonly record struct FlowSource
{
    private FlowSource(FlowSourceKind kind, NodePath? node, PortName? port, string? text)
    {
        Kind = kind;
        Node = node;
        Port = port;
        Text = text;
    }

    /// <summary>终点种类。</summary>
    public FlowSourceKind Kind { get; }

    /// <summary>来源节点，开始端口与默认值为空。</summary>
    public NodePath? Node { get; }

    /// <summary>来源端口，默认值为空。</summary>
    public PortName? Port { get; }

    /// <summary>装配期确定的文本。</summary>
    public string? Text { get; }

    /// <summary>是否无法解析。</summary>
    public bool IsMissing => Kind == FlowSourceKind.Missing;

    /// <summary>是否在任务内确定一次的来源。</summary>
    public bool IsStatic => Kind is FlowSourceKind.StartFixed
        or FlowSourceKind.StartInput
        or FlowSourceKind.NodeConstant
        or FlowSourceKind.Default
        or FlowSourceKind.RuntimeConstant;

    /// <summary>来源文本。</summary>
    public string Display => Kind switch
    {
        FlowSourceKind.StartFixed or FlowSourceKind.StartInput => $"@{Port}",
        FlowSourceKind.NodeOutput or FlowSourceKind.NodeConstant or FlowSourceKind.RuntimeConstant => $"{Node}@{Port}",
        FlowSourceKind.Default => "输入默认值",
        _ => "缺失",
    };

    /// <summary>无法解析的来源。</summary>
    public static FlowSource Missing() => new(FlowSourceKind.Missing, null, null, null);

    /// <summary>开始端口的固定值。</summary>
    public static FlowSource StartFixed(PortName port, string text) =>
        new(FlowSourceKind.StartFixed, null, port, text);

    /// <summary>开始端口的注入值。</summary>
    public static FlowSource StartInput(PortName port) =>
        new(FlowSourceKind.StartInput, null, port, null);

    /// <summary>节点动态端口产出。</summary>
    public static FlowSource NodeOutput(NodePath node, PortName port) =>
        new(FlowSourceKind.NodeOutput, node, port, null);

    /// <summary>节点常量端口的固定值。</summary>
    public static FlowSource NodeConstant(NodePath node, PortName port, string text) =>
        new(FlowSourceKind.NodeConstant, node, port, text);

    /// <summary>输入端口默认值。</summary>
    public static FlowSource Default(string text) => new(FlowSourceKind.Default, null, null, text);

    /// <summary>节点唯一一次执行产出的运行期常量。</summary>
    public static FlowSource RuntimeConstant(NodePath node, PortName port) =>
        new(FlowSourceKind.RuntimeConstant, node, port, null);
}

/// <summary>来源终点的种类。</summary>
public enum FlowSourceKind
{
    /// <summary>无法解析。</summary>
    Missing,

    /// <summary>开始端口的固定值。</summary>
    StartFixed,

    /// <summary>开始端口的注入值。</summary>
    StartInput,

    /// <summary>节点动态端口产出。</summary>
    NodeOutput,

    /// <summary>节点常量端口的固定值。</summary>
    NodeConstant,

    /// <summary>输入端口默认值。</summary>
    Default,

    /// <summary>节点唯一一次执行产出的运行期常量。</summary>
    RuntimeConstant,
}