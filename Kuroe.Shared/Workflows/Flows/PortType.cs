namespace Kuroe.Shared.Workflows.Flows;

/// <summary>端口类型：端口承载什么产出，以及这份产出怎么被取用。
/// 输出端口取 Data、Items、Context，输入端口取 Data、Context、Signal。</summary>
public enum PortType
{
    /// <summary>数据：产出按序注入目标上下文，参与齐备判定。</summary>
    Data,

    /// <summary>条目票：只驱动按条目展开，不注入文本。仅输出端口声明。</summary>
    Items,

    /// <summary>上下文：产出置于目标上下文最前，与系统指令构成稳定前缀。</summary>
    Context,

    /// <summary>触发信号：不消费产出，只以来源的新版本驱动启动。仅输入端口声明。</summary>
    Signal,
}
