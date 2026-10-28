namespace Kuroe.Flow.Shared.StaticGraph.PortModel;

/// <summary>静态端口的值来源：常量文本或宿主开始端口，二选一。
/// 常量在装载期折叠，宿主开始端口在任务启动时由宿主值解析。</summary>
public readonly record struct StaticValue
{
    private StaticValue(string? text, PortName? hostPort)
    {
        Text = text;
        HostPort = hostPort;
    }

    /// <summary>常量文本，宿主来源时为空。</summary>
    public string? Text { get; }

    /// <summary>宿主开始端口，常量来源时为空。</summary>
    public PortName? HostPort { get; }

    /// <summary>常量值。</summary>
    public static StaticValue Constant(string text) => new(text, null);

    /// <summary>由宿主在任务启动时给出的开始端口值。</summary>
    public static StaticValue Host(PortName port) => new(null, port);

    /// <summary>取解析后的文本：常量直接给出，宿主来源取给定查询，未给出时为空。</summary>
    public string? Resolve(Func<PortName, string?> host) => Text ?? (HostPort is { } port ? host(port) : null);
}