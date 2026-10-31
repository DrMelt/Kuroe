using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>端口表的查找、交回判定与声明文本。</summary>
public static class PortRules
{
    /// <summary>执行需经契约工具交回的端口类型：Text 交回内容，Signal 交回端口名或触发值；Context 由来源透传，不需要交回。
    /// 不承载触发的端口交回时触发值恒为 true。</summary>
    public static bool RequiresSubmission(PortType type) =>
        type.Semantics is PortSemantics.Text or PortSemantics.Signal;

    /// <summary>输出端口是否需要执行交回：动态端口按类型判定，常量端口只在由执行产出时交回。</summary>
    public static bool RequiresSubmission(Port port, NodeEmission emission) =>
        port.Mode == PortMode.Constant
            ? IsRuntimeConstant(port, emission)
            : RequiresSubmission(port.Type);

    /// <summary>端口是否承载文本内容。</summary>
    public static bool CarriesText(PortType type) => type.Semantics == PortSemantics.Text;

    /// <summary>常量端口是否由执行产出：常量端口没有固定值与来源引用，且节点产出模式为首次发布后冻结。</summary>
    public static bool IsRuntimeConstant(Port port, NodeEmission emission) =>
        port.Mode == PortMode.Constant && port.Value is null && port.Ref is null && emission == NodeEmission.Once;

    /// <summary>输出端口表里执行需交回的端口，按声明序。</summary>
    public static IReadOnlyList<Port> RequiringSubmission(IReadOnlyList<Port> ports, NodeEmission emission) =>
        [.. ports.Where(port => RequiresSubmission(port, emission))];

    /// <summary>执行可交回的端口，按声明序：全部动态端口与运行期常量端口。</summary>
    public static IReadOnlyList<Port> SubmittablePorts(IReadOnlyList<Port> ports, NodeEmission emission) =>
        [.. ports.Where(port => port.Mode != PortMode.Constant || IsRuntimeConstant(port, emission))];

    /// <summary>可交回端口表里本轮必须交回的端口：动态 Text 与 Signal，以及全部运行期常量端口。</summary>
    public static IReadOnlyList<Port> RequiredFromSubmittable(IReadOnlyList<Port> ports) =>
        [.. ports.Where(port => port.Mode == PortMode.Constant || RequiresSubmission(port.Type))];

    /// <summary>端口声明的清单，按声明序，有说明写 端口名：说明。</summary>
    public static string List(IEnumerable<Port> ports) => string.Join('、', ports.Select(Describe));

    /// <summary>单个端口的写法：有说明写 端口名：说明，未写说明只写端口名。</summary>
    public static string Describe(Port port) =>
        port.Description is { Length: > 0 } description ? $"{port.Name}：{description}" : port.Name.Value;

    /// <summary>按名字取输出端口声明，未声明时为空。</summary>
    public static Port? Find(this IReadOnlyList<Port> ports, PortName name)
    {
        foreach (Port port in ports)
        {
            if (port.Name == name)
            {
                return port;
            }
        }

        return null;
    }

    /// <summary>按名字取输入端口声明，未声明时为空。</summary>
    public static InputPort? Find(this IReadOnlyList<InputPort> ports, PortName name)
    {
        foreach (InputPort port in ports)
        {
            if (port.Name == name)
            {
                return port;
            }
        }

        return null;
    }
}

