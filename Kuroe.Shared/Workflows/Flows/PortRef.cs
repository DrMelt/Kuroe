namespace Kuroe.Shared.Workflows.Flows;

/// <summary>端口引用：来源名与端口名。来源为空表示库容器成员对传入端口的绑定引用（@端口），
/// 端口名来自来源节点的输出端口表：保留的拆分与上下文输出端口、隐式 ContextOutput 或声明端口。</summary>
public readonly record struct PortRef(NodeName? Source, PortName Port)
{
    /// <summary>绑定端口引用：容器成员经 @端口 取传入端口。</summary>
    public static PortRef Bound(PortName port) => new(null, port);

    /// <summary>命名端口的引用：来源@端口。</summary>
    public static PortRef Named(NodeName source, PortName port) => new(source, port);

    /// <summary>装配上下文的引用：来源@ContextOutput。</summary>
    public static PortRef Context(NodeName source) => new(source, PortNames.ContextOutput);

    /// <summary>引用文本：绑定端口写 @端口，其余一律写 来源@端口。</summary>
    public string Display => Source is { } source ? $"{source}@{Port.Value}" : $"@{Port.Value}";
}
