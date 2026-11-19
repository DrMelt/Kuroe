namespace Kuroe.Document.Shared;

/// <summary>输出端口条目：所有节点共用。Type 是已注册的端口类型名，省略时按 Text，Mode 省略时按类型默认模式。Value 是固定值，Ref 是同一节点内部作用域中的来源端口，两者互斥。
/// Default 只用于开始端口，是提交输入的预填值与省略时的回退值，端口由宿主给值时不再生效。
/// Mode 取 Plain、Trigger 或 Constant，支持范围由端口类型注册声明。Constant 端口的值在任务内确定一次，只供下游 Bindings 读取，不进入 From；
/// 没有 Value 与 Ref 时由 Emission 为 Once 的节点执行产出。</summary>
public sealed record PortDocument(
    string Name,
    string? Type = null,
    PortModeDocument? Mode = null,
    string? Description = null,
    string? Value = null,
    string? Ref = null,
    string? Default = null);