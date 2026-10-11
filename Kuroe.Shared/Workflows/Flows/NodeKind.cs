namespace Kuroe.Shared.Workflows.Flows;

/// <summary>节点类型：类型由程序定义，文件只能选用且必须显式声明，引用节点的类型由被引用定义决定。
/// 节点按类型声明各自允许的字段，产出一律经声明的输出端口。</summary>
public enum NodeKind
{
    /// <summary>容器：组织成员节点并限定作用域，自身不执行。</summary>
    Container,

    /// <summary>执行节点：由模型执行，收口时交回端口输出。</summary>
    Executable,

    /// <summary>输入节点：不启动 run，用户回答即端口输出。</summary>
    Input,
}
