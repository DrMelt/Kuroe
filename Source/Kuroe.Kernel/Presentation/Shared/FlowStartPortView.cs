namespace Kuroe.Presentation.Shared;

/// <summary>展示用开始端口：端口形状、输入来源与宿主绑定。</summary>
public sealed record FlowStartPortView(
    string Name,
    string Kind,
    string? Description,
    string? Default,
    string? Value,
    FlowStartKind InputKind,
    string? HostInput)
{
    /// <summary>开始端口是否为运行期常量。</summary>
    public bool Constant { get; init; }

    /// <summary>开始端口的一行展示文本。</summary>
    public string Label
    {
        get
        {
            string text = Name;
            if (Value is { Length: > 0 } value)
            {
                text = $"{Name}={value}";
            }
            else if (Default is { Length: > 0 } defaultValue)
            {
                text = $"{Name}，默认 {defaultValue}";
            }

            if (HostInput is { Length: > 0 } input)
            {
                text = $"{text}←{input}";
            }

            return Constant ? $"{text}·常量" : text;
        }
    }
}