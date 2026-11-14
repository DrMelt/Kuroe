using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Node.Shared.StaticGraph.Graph;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;

/// <summary>一次激活的输入：按输入端口给出本次取到的来源产出，保留绑定顺序与端口类型。输入端口文本统一经 <see cref="Text"/> 读取。
/// 节点按 <see cref="INodeInputs"/> 读取内容，连接状态只供执行装配使用。</summary>
public sealed class NodeInputValues : INodeInputs
{
    /// <summary>没有输入的激活使用的空值。</summary>
    public static NodeInputValues Empty { get; } = new([], new HashSet<PortName>());

    private readonly IReadOnlyList<NodeInputValue> _ordered;
    private readonly Dictionary<PortName, IReadOnlyList<ContextValue>> _values;
    private readonly IReadOnlySet<PortName> _connected;
    private readonly IReadOnlyDictionary<PortName, string> _texts;

    /// <summary>按绑定顺序给出取值，已连接端口用于区分未连接的默认值，
    /// 静态端口文本并入端口文本表，不进入动态取值。</summary>
    public NodeInputValues(
        IReadOnlyList<NodeInputValue> ordered,
        IReadOnlySet<PortName> connected,
        IPortTexts? staticTexts = null)
    {
        _ordered = ordered;
        _connected = connected;
        IPortTexts staticTable = staticTexts ?? PortTexts.Empty;

        Dictionary<PortName, List<ContextValue>> values = [];
        foreach (NodeInputValue value in ordered)
        {
            if (!values.TryGetValue(value.TargetInput, out List<ContextValue>? list))
            {
                list = [];
                values[value.TargetInput] = list;
            }

            list.Add(value.Value);
        }

        _values = values.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<ContextValue>)pair.Value);
        _texts = BuildTexts(_values, staticTable);
    }

    /// <summary>按绑定顺序排列的取值，每条给出目标端口、端口类型与取值。</summary>
    public IReadOnlyList<NodeInputValue> Ordered => _ordered;

    /// <summary>该输入端口是否接了来源。信号输入也计入连接，但不取内容。执行装配按它区分未连接端口与空产出。</summary>
    public bool Connected(PortName input) => _connected.Contains(input);

    /// <summary>该输入端口的全部取值，端口没有来源时为空。</summary>
    public IReadOnlyList<ContextValue> Values(PortName input) =>
        _values.TryGetValue(input, out IReadOnlyList<ContextValue>? values) ? values : [];

    /// <summary>该输入端口的文本表：动态取值按绑定顺序以空行连接，静态端口给静态值。</summary>
    public IReadOnlyDictionary<PortName, string> Texts => _texts;

    /// <summary>该输入端口的文本，未装配时为空。</summary>
    public string? Text(PortName port) => Texts.GetValueOrDefault(port);

    /// <summary>附加本次激活的静态端口文本，没有静态值时保持原实例。</summary>
    public NodeInputValues WithStatic(IPortTexts staticTexts) =>
        staticTexts.Texts.Count == 0 ? this : new(_ordered, _connected, staticTexts);

    /// <summary>按动态取值与静态值合成端口文本表，静态值优先。</summary>
    private static Dictionary<PortName, string> BuildTexts(
        IReadOnlyDictionary<PortName, IReadOnlyList<ContextValue>> values,
        IPortTexts staticTexts)
    {
        IReadOnlyDictionary<PortName, string> staticTable = staticTexts.Texts;
        Dictionary<PortName, string> texts = [];
        foreach ((PortName port, IReadOnlyList<ContextValue> list) in values)
        {
            if (!staticTable.ContainsKey(port) && ContextValues.Join(list) is { } text)
            {
                texts[port] = text;
            }
        }

        foreach ((PortName port, string text) in staticTable)
        {
            texts[port] = text;
        }

        return texts;
    }

    /// <summary>按一组绑定读取来源产出：非驱动绑定不进入输入，信号不取内容，其余按绑定顺序取值。</summary>
    public static NodeInputValues From(InputGroup group, INodeContext context)
    {
        List<NodeInputValue> ordered = [];
        HashSet<PortName> connected = [];
        foreach (InputBinding binding in group.Bindings)
        {
            if (!binding.Drives || binding.TargetInput is not { } input)
            {
                continue;
            }

            connected.Add(input);
            if (binding.Signal || binding.SourcePort is null)
            {
                continue;
            }

            foreach (ContextValue value in context.OutputValues(binding.Source))
            {
                ordered.Add(new NodeInputValue(input, binding.Type ?? PortTypes.Text, value));
            }
        }

        return new NodeInputValues(ordered, connected);
    }
}

/// <summary>一条输入取值：进入哪个输入端口、按什么端口类型解释、取值本身。</summary>
public sealed record NodeInputValue(PortName TargetInput, PortType Type, ContextValue Value);