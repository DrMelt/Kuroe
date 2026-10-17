namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>一条上游连接：目标输入端口与来源端口引用。取用方式由目标输入端口的声明决定，
/// 来源写 来源@端口 指向来源节点的输出端口，库容器成员可写 @端口 引用传入端口。</summary>
public sealed record FromLink(PortName Input, PortRef Ref);

/// <summary>一组启动条件：组内输入全部齐备该组才成立，组间任一组成立即满足启动依赖。
/// 组是一套完整的输入绑定方案，覆盖目标声明的全部输入端口。</summary>
public sealed record FromGroup(IReadOnlyList<FromLink> Links);
