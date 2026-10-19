namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>端口类型：端口承载什么产出，以及这份产出怎么被取用。
/// 输出端口取 Text、Context，输入端口取 Text、Context、Signal。</summary>
public enum PortType
{
    /// <summary>文本：产出按序注入目标输入，参与齐备判定。</summary>
    Text,

    /// <summary>上下文：产出置于目标输入最前，与系统指令构成稳定前缀。</summary>
    Context,

    /// <summary>触发信号：不消费产出，只以来源的新版本驱动启动。仅输入端口声明。</summary>
    Signal,
}
