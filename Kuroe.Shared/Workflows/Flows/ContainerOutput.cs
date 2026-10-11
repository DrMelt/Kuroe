namespace Kuroe.Shared.Workflows.Flows;

/// <summary>容器命名输出端口声明：绑定目标与说明。种类取自被绑定的成员端口，不在此声明。</summary>
/// <param name="Ref">容器子树内的成员引用，写 成员@端口。</param>
/// <param name="Description">端口透出什么内容的说明，未写时为空。</param>
public readonly record struct ContainerOutput(PortRef Ref, string? Description = null);
