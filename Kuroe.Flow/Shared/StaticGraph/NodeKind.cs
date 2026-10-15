namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>节点类型名：流程文件里 Kind 的取值，也是节点类型注册表的键。
/// 类型不再是程序内固定的一小组，注册了哪种类型就允许哪种取值。</summary>
public readonly record struct NodeKind(string Value)
{
    /// <summary>类型名的文本形式。</summary>
    public override string ToString() => Value;
}
