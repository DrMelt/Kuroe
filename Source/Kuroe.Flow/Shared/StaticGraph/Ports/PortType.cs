namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>注册后的端口类型：名字、语义与是否为 Trigger 版。端口类型只能经 <see cref="PortTypes.Register"/> 产生，
/// 注册一次同时产生普通版与 Trigger 版。Trigger 版在交回时额外携带是否触发下游的布尔值。</summary>
public readonly record struct PortType
{
    internal PortType(string name, PortSemantics semantics, bool isTrigger)
    {
        Name = name;
        Semantics = semantics;
        IsTrigger = isTrigger;
    }

    /// <summary>类型名，流程文件里的 Type 取值。</summary>
    public string Name { get; }

    /// <summary>端口语义，决定产出如何进入目标输入。</summary>
    public PortSemantics Semantics { get; }

    /// <summary>是否为 Trigger 版：交回时携带触发值，启动组按其值决定是否启动。</summary>
    public bool IsTrigger { get; }

    /// <summary>类型名的文本形式。</summary>
    public override string ToString() => Name;
}
