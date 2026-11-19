namespace Kuroe.Executor.Shared.Nodes;

/// <summary>执行器条目的种类名，与流程文件里 Kind 的取值对应。种类名唯一，比较按原文。</summary>
public readonly record struct ExecutorKind(string Value)
{
    /// <summary>种类名的文本形式。</summary>
    public override string ToString() => Value;
}