namespace Kuroe.Flow.Execution;

/// <summary>执行输入端口在本次执行中的角色。</summary>
public enum ExecutionPortRole
{
    /// <summary>作为执行内容参与本轮执行。</summary>
    Content,

    /// <summary>按名字供执行参数读取，不进入执行内容。</summary>
    Configuration,

    /// <summary>只驱动启动，不承载内容。</summary>
    Control,
}