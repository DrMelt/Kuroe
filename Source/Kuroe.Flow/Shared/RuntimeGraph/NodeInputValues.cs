using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>一次激活的输入：按输入端口给出本次取到的来源产出，保留绑定顺序与端口类型。输入端口文本统一经 <see cref="Text"/> 读取。</summary>
public sealed class NodeInputValues : IPortTexts
{
    /// <summary>没有输入的激活使用的空值。</summary>
    public static NodeInputValues Empty { get; } = new([], new HashSet<PortName>());

    private readonly IReadOnlyList<NodeInputValue> _ordered;
    private readonly Dictionary<PortName, IReadOnlyList<ContextValue>> _values;
    private readonly IReadOnlySet<PortName> _connected;
    private readonly IReadOnlyDictionary<PortName, bool> _triggers;
    private readonly IPortTexts _staticTexts;
    private readonly IReadOnlyDictionary<PortName, string> _texts;

    /// <summary>按绑定顺序给出取值，已连接端口用于区分未连接的默认值，触发值按输入端口给出，
    /// 静态端口文本单独存放，不并入动态取值。</summary>
    public NodeInputValues(
        IReadOnlyList<NodeInputValue> ordered,
        IReadOnlySet<PortName> connected,
        IReadOnlyDictionary<PortName, bool>? triggers = null,
        IPortTexts? staticTexts = null)
    {
        _ordered = ordered;
        _connected = connected;
        _triggers = triggers ?? new Dictionary<PortName, bool>();
        _staticTexts = staticTexts ?? PortTexts.Empty;

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
        _texts = BuildTexts(_values, _staticTexts);
    }

    /// <summary>按绑定顺序排列的取值，每条给出目标端口、端口类型与取值。</summary>
    public IReadOnlyList<NodeInputValue> Ordered => _ordered;

    /// <summary>该输入端口是否接了来源。信号输入也计入连接，但不取内容。</summary>
    public bool Connected(PortName input) => _connected.Contains(input);

    /// <summary>该输入端口的全部取值，端口没有来源时为空。</summary>
    public IReadOnlyList<ContextValue> Values(PortName input) =>
        _values.TryGetValue(input, out IReadOnlyList<ContextValue>? values) ? values : [];

    /// <summary>该输入端口的文本表：动态取值按绑定顺序以空行连接，静态端口给静态值。</summary>
    public IReadOnlyDictionary<PortName, string> Texts => _texts;

    /// <summary>该输入端口的文本，未装配时为空。</summary>
    public string? Text(PortName port) => Texts.GetValueOrDefault(port);

    /// <summary>本次激活的静态端口文本视图，装配执行输入时透传，没有静态值时为空视图。输入读取统一经 <see cref="IPortTexts.Text"/>。</summary>
    public IPortTexts StaticTexts => _staticTexts;

    /// <summary>附加本次激活的静态端口文本视图，没有静态值时保持原实例。</summary>
    public NodeInputValues WithStatic(IPortTexts staticTexts) =>
        staticTexts.Texts.Count == 0 ? this : new(_ordered, _connected, _triggers, staticTexts);

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

    /// <summary>该输入端口最近一次绑定的触发值，没有触发值的端口为 true。</summary>
    public bool Triggers(PortName input) => _triggers.GetValueOrDefault(input, true);

    /// <summary>按一组绑定读取来源产出：信号不取内容，其余按绑定顺序取值，每个输入端口的触发值取最后一次绑定。</summary>
    public static NodeInputValues From(InputGroup group, INodeContext context)
    {
        List<NodeInputValue> ordered = [];
        HashSet<PortName> connected = [];
        Dictionary<PortName, bool> triggers = [];
        foreach (InputBinding binding in group.Bindings)
        {
            if (binding.TargetInput is not { } input)
            {
                continue;
            }

            connected.Add(input);
            triggers[input] = binding.Triggers(context.Source(binding.Source));
            if (binding.Signal || binding.Port is not { } port)
            {
                continue;
            }

            foreach (ContextValue value in context.Runtime(binding.Source).OutputValues(context, port))
            {
                ordered.Add(new NodeInputValue(input, binding.Type ?? PortTypes.Text, value));
            }
        }

        return new NodeInputValues(ordered, connected, triggers);
    }
}

/// <summary>一条输入取值：进入哪个输入端口、按什么端口类型解释、取值本身。</summary>
public sealed record NodeInputValue(PortName TargetInput, PortType Type, ContextValue Value);