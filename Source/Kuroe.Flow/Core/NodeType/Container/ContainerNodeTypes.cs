using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.NodeType.Container;

/// <summary>引擎内置的容器类型：类型名、静态形状与描述实例。容器组织成员并限定作用域，自身不执行，运行期对象由引擎建立。</summary>
public static class ContainerNodeTypes
{
    /// <summary>类型名，与流程文件里 Kind 的取值对应。</summary>
    public static NodeKind Name { get; } = new("Container");

    /// <summary>容器类型的静态形状：组织子节点，不派发执行，输入端口声明由引用处绑定。</summary>
    public static NodeTypeShape Shape { get; } = new()
    {
        Organizes = true,
        InputTypes = [PortType.Text, PortType.Context, PortType.Signal],
    };

    /// <summary>容器描述实例，全引擎共用一份。</summary>
    public static INodeType Type { get; } = new ContainerNodeType();
}