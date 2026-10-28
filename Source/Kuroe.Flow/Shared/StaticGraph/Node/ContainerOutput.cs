using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.StaticGraph.Node;

/// <summary>容器命名输出端口声明：绑定目标与说明。类型取自被绑定的成员端口，不在此声明。</summary>
/// <param name="Ref">容器子树内的成员引用，写 成员@端口。</param>
/// <param name="Description">端口透出什么内容的说明，未写时为空。</param>
/// <param name="IsStatic">静态输出端口：转发成员静态端口，供下游 Static 绑定读取。</param>
public readonly record struct ContainerOutput(PortRef Ref, string? Description = null, bool IsStatic = false);
