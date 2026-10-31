namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>端口类型注册表：登记类型、语义与允许模式，模式不再生成类型名后缀。
/// 内置 Text、Context 与 Signal 三种语义，各自声明支持的模式。类型名唯一，解析不区分大小写。
/// 注册属于装配前准备，运行期只读。</summary>
public static class PortTypes
{
    private static readonly Dictionary<string, PortTypeDefinition> ByName = new(StringComparer.OrdinalIgnoreCase);

    static PortTypes()
    {
        Text = Register("Text", PortSemantics.Text, PortMode.Plain,
            PortMode.Plain, PortMode.Trigger, PortMode.Constant);
        Context = Register("Context", PortSemantics.Context, PortMode.Plain,
            PortMode.Plain, PortMode.Constant);
        Signal = Register("Signal", PortSemantics.Signal, PortMode.Trigger,
            PortMode.Plain, PortMode.Trigger, PortMode.Constant);
    }

    /// <summary>文本端口。</summary>
    public static PortType Text { get; }

    /// <summary>上下文端口。</summary>
    public static PortType Context { get; }

    /// <summary>触发信号端口。</summary>
    public static PortType Signal { get; }

    /// <summary>登记一个端口类型，声明默认模式与允许的模式集合。模式集合不能为空，默认模式必须在集合内。</summary>
    public static PortType Register(
        string name,
        PortSemantics semantics,
        PortMode defaultMode,
        params PortMode[] modes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("端口类型名不能为空。", nameof(name));
        }

        PortMode[] supported = [.. modes.Distinct()];
        if (supported.Length == 0)
        {
            throw new ArgumentException("端口类型必须声明至少一种模式。", nameof(modes));
        }

        if (!supported.Contains(defaultMode))
        {
            throw new ArgumentException("端口类型的默认模式必须在支持模式内。", nameof(defaultMode));
        }

        PortType type = new(name, semantics);
        PortTypeDefinition definition = new(type, defaultMode, supported.ToHashSet());
        if (!ByName.TryAdd(name, definition))
        {
            throw new InvalidOperationException($"端口类型 {name} 重复注册。");
        }

        return type;
    }

    /// <summary>按名字取端口类型，未注册时返回 false。</summary>
    public static bool TryParse(string? name, out PortType type)
    {
        if (name is not null && ByName.TryGetValue(name, out PortTypeDefinition? found))
        {
            type = found.Type;

            return true;
        }

        type = default;

        return false;
    }

    /// <summary>取类型的注册项，未注册时返回 false。</summary>
    public static bool TryGetDefinition(PortType type, out PortTypeDefinition definition)
    {
        if (type.Name is null)
        {
            definition = default!;

            return false;
        }

        if (ByName.TryGetValue(type.Name, out PortTypeDefinition? found) && found.Type == type)
        {
            definition = found;

            return true;
        }

        definition = default!;

        return false;
    }

    /// <summary>类型是否允许该模式，未注册时返回 false。</summary>
    public static bool Supports(PortType type, PortMode mode) =>
        TryGetDefinition(type, out PortTypeDefinition definition) && definition.Modes.Contains(mode);

    /// <summary>类型未写模式时的默认模式，未注册时按普通模式。</summary>
    public static PortMode DefaultMode(PortType type) =>
        TryGetDefinition(type, out PortTypeDefinition definition) ? definition.DefaultMode : PortMode.Plain;
}