namespace Kuroe.Execution.Shared;

/// <summary>执行种类，用于选择运行器与恢复器。内容由节点类型名或装配常量给出，执行层不解释。</summary>
public readonly record struct ExecutionKind(string Value)
{
    /// <summary>通配种类，没有同种类注册时使用。</summary>
    public static ExecutionKind Any { get; } = new("*");

    /// <summary>执行种类的文本形式。</summary>
    public override string ToString() => Value;
}