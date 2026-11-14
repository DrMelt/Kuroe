namespace Kuroe.Execution.Shared;

/// <summary>执行端口承载的内容种类。</summary>
public enum ExecutionPortKind
{
    /// <summary>文本：按序进入执行输入。</summary>
    Text,

    /// <summary>上下文：置于执行输入最前。</summary>
    Context,

    /// <summary>触发信号：不承载内容，只表达触发位。</summary>
    Signal,
}