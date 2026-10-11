namespace Kuroe.Shared.Workflows.Flows;

/// <summary>端口的接线种类，决定这个端口怎么被消费与校验。</summary>
public enum PortKind
{
    /// <summary>文本命名端口：模型经契约工具交回命名段，注入目标上下文。</summary>
    Data,

    /// <summary>拆分支票端口：结构化条目，驱动按条目展开，不注入文本。</summary>
    Items,

    /// <summary>装配上下文端口：来源装配上下文逐条保留出处注入目标最前。</summary>
    Context,
}
