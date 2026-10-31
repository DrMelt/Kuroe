namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>执行节点的启动方式。启动方式由输入侧方案显式给出，不从空组或组号推断。</summary>
public enum NodeStartMode
{
    /// <summary>由动态来源的新版本驱动启动组。</summary>
    Driven,

    /// <summary>输入全部来自常量，静态输入就绪后执行一次。</summary>
    StaticOnly,
}
