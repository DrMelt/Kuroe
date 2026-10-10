namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程协议的保留端口名。供 From 引用与端口声明校验使用，不可作命名输出端口声明。</summary>
public static class PortNames
{
    /// <summary>拆分端口：规划执行节点的结构化拆分支票，PerItem 目标经「来源@拆分」取条目展开。</summary>
    public static PortName Split { get; } = new("拆分");

    /// <summary>上下文输出端口：每个执行节点无需声明即具备，From 用「来源@ContextOutput」消费。</summary>
    public static PortName ContextOutput { get; } = new("ContextOutput");

    /// <summary>上下文输入端口：From 条目的 Context 标记承载语义，端口名不可作输出端口声明。</summary>
    public static PortName ContextInput { get; } = new("ContextInput");
}
