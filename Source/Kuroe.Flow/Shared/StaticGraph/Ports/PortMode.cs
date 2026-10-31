namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>端口模式：普通动态、触发动态或常量。模式互斥，由端口类型的注册声明支持范围。</summary>
public enum PortMode
{
    /// <summary>普通动态端口。</summary>
    Plain,

    /// <summary>触发动态端口，交回时携带触发值，启动组按触发值门控。</summary>
    Trigger,

    /// <summary>常量端口，任务内只确定一次值，只供 Static 读取。</summary>
    Constant,
}
