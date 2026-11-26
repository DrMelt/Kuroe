using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Flow.Graph.Shared.StaticGraph;

/// <summary>静态输入与常量输出解析的终点。<see cref="StaticSourceKind.Missing"/> 带错误说明，其余带固定值、默认值、开始端口或运行期常量来源。</summary>
public readonly record struct StaticResolution(
    StaticSourceKind Kind,
    BindingSource Source,
    PortName Port,
    string? Text,
    string? Error)
{
    /// <summary>开始端口的固定值。</summary>
    public static StaticResolution StartFixed(PortName port, string text) =>
        new(StaticSourceKind.StartFixed, BindingSource.ForStart(port), port, text, null);

    /// <summary>开始端口的非固定值：宿主给值或提交输入。</summary>
    public static StaticResolution StartInjected(PortName port) =>
        new(StaticSourceKind.StartInjected, BindingSource.ForStart(port), port, null, null);

    /// <summary>常量输出的字面量。</summary>
    public static StaticResolution Literal(NodePath node, PortName port, string text) =>
        new(StaticSourceKind.Literal, BindingSource.ForNode(node), port, text, null);

    /// <summary><see cref="NodeEmission.Once"/> 节点唯一一次执行产出的常量。</summary>
    public static StaticResolution RuntimeConstant(NodePath node, PortName port) =>
        new(StaticSourceKind.RuntimeConstant, BindingSource.ForNode(node), port, null, null);

    /// <summary>静态输入的默认值。</summary>
    public static StaticResolution Default(NodePath node, PortName port, string text) =>
        new(StaticSourceKind.Default, BindingSource.ForNode(node), port, text, null);

    /// <summary>无法解析，给出错误说明。</summary>
    public static StaticResolution Missing(NodePath node, PortName port, string error) =>
        Missing(BindingSource.ForNode(node), port, error);

    /// <summary>无法解析，给出错误说明。</summary>
    public static StaticResolution Missing(BindingSource source, PortName port, string error) =>
        new(StaticSourceKind.Missing, source, port, null, error);
}

/// <summary>静态输入与常量输出解析终点的种类。</summary>
public enum StaticSourceKind
{
    /// <summary>开始端口的固定值。</summary>
    StartFixed,

    /// <summary>开始端口的非固定值，来自宿主给值或提交输入。</summary>
    StartInjected,

    /// <summary>常量输出的字面量。</summary>
    Literal,

    /// <summary><see cref="NodeEmission.Once"/> 节点唯一一次执行产出的常量。</summary>
    RuntimeConstant,

    /// <summary>静态输入的默认值。</summary>
    Default,

    /// <summary>无法解析。</summary>
    Missing,
}