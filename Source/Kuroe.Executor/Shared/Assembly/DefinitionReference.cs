using Kuroe.Executor.Shared.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Executor.Shared.Assembly;

/// <summary>端口引用：来源标签与端口名。绑定端口由所在容器的绑定域展开，开始端口引用直接落到流程端口。
/// 类型不随引用走，由来源端口表在装配期解析。</summary>
public readonly record struct DefinitionReference
{
    private DefinitionReference(string? source, PortName port, bool isStart)
    {
        Source = source;
        Port = port;
        IsStart = isStart;
    }

    /// <summary>来源标签，绑定端口与开始端口为空。</summary>
    public string? Source { get; }

    /// <summary>端口名。</summary>
    public PortName Port { get; }

    /// <summary>是否为流程开始端口引用。</summary>
    public bool IsStart { get; }

    /// <summary>绑定端口引用：容器成员经 @端口 取传入端口。</summary>
    public static DefinitionReference Bound(PortName port) => new(null, port, isStart: false);

    /// <summary>流程开始端口引用：根作用域的 @端口。</summary>
    public static DefinitionReference Start(PortName port) => new(null, port, isStart: true);

    /// <summary>命名端口的引用：来源@端口。</summary>
    public static DefinitionReference Named(string source, PortName port) => new(source, port, isStart: false);

    /// <summary>引用文本：开始端口与绑定端口都写 @端口，其余写 来源@端口。</summary>
    public string Display => Source is { } source ? $"{source}@{Port.Value}" : $"@{Port.Value}";
}