namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>端口类型注册表：按名字登记类型，注册一次自动产生普通版与 Trigger 版。
/// 内置 Text、Context 与 Signal 三种语义，每种语义各产生一对类型。类型名唯一，解析不区分大小写。
/// 注册属于装配前准备，运行期只读。</summary>
public static class PortTypes
{
    private static readonly Dictionary<string, PortType> ByName = new(StringComparer.OrdinalIgnoreCase);

    static PortTypes()
    {
        PortTypePair text = Register("Text", PortSemantics.Text);
        Text = text.Plain;
        TextTrigger = text.Trigger;

        PortTypePair context = Register("Context", PortSemantics.Context);
        Context = context.Plain;
        ContextTrigger = context.Trigger;

        PortTypePair signal = Register("Signal", PortSemantics.Signal);
        Signal = signal.Plain;
        SignalTrigger = signal.Trigger;
    }

    /// <summary>文本端口，普通版。</summary>
    public static PortType Text { get; }

    /// <summary>文本端口，Trigger 版。</summary>
    public static PortType TextTrigger { get; }

    /// <summary>上下文端口，普通版。</summary>
    public static PortType Context { get; }

    /// <summary>上下文端口，Trigger 版。Context 不交回，内置节点不放行这个类型。</summary>
    public static PortType ContextTrigger { get; }

    /// <summary>触发信号端口，普通版。</summary>
    public static PortType Signal { get; }

    /// <summary>触发信号端口，Trigger 版。</summary>
    public static PortType SignalTrigger { get; }

    /// <summary>注册一种端口类型，自动产生普通版与 Trigger 版。类型名与生成的 Trigger 名都必须未被占用。</summary>
    public static PortTypePair Register(string name, PortSemantics semantics)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("端口类型名不能为空。", nameof(name));
        }

        if (name.EndsWith("Trigger", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("端口类型名不能以 Trigger 结尾。", nameof(name));
        }

        PortType plain = Add(name, semantics, isTrigger: false);
        PortType trigger = Add($"{name}Trigger", semantics, isTrigger: true);

        return new PortTypePair(plain, trigger);
    }

    /// <summary>按名字取端口类型，未注册时返回 false。</summary>
    public static bool TryParse(string? name, out PortType type)
    {
        if (name is not null && ByName.TryGetValue(name, out PortType found))
        {
            type = found;

            return true;
        }

        type = default;

        return false;
    }

    /// <summary>登记一个类型，名字重复时报错。</summary>
    private static PortType Add(string name, PortSemantics semantics, bool isTrigger)
    {
        PortType type = new(name, semantics, isTrigger);
        if (!ByName.TryAdd(name, type))
        {
            throw new InvalidOperationException($"端口类型 {name} 重复注册。");
        }

        return type;
    }
}