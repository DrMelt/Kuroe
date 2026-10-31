namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>注册后的端口类型：名字与语义。模式由端口声明选择，支持范围由类型注册声明。
/// 端口类型只能经 <see cref="PortTypes.Register"/> 产生。</summary>
public readonly record struct PortType
{
    internal PortType(string name, PortSemantics semantics)
    {
        Name = name;
        Semantics = semantics;
    }

    /// <summary>类型名，流程文件里的 Type 取值。</summary>
    public string Name { get; }

    /// <summary>端口语义，决定动态产出如何进入目标输入。</summary>
    public PortSemantics Semantics { get; }

    /// <summary>类型名的文本形式。</summary>
    public override string ToString() => Name;
}
