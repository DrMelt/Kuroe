using Kuroe.Flow.InputChannel;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Node.Input;

/// <summary>输入节点类型的对外入口：类型名、静态形状与实例建立。输入节点不启动 run，用户回答即端口输出。</summary>
public static class InputNodeTypes
{
    /// <summary>类型名，与流程文件里 Kind 的取值对应。</summary>
    public static NodeKind Name { get; } = new("Input");

    /// <summary>输入节点类型的静态形状：不派发 run，输入侧只作驱动与内容引用，输出侧只写回答。</summary>
    public static NodeTypeShape Shape { get; } = new()
    {
        StartsUnaided = true,
        InputTypes = [PortType.Text, PortType.Signal],
        OutputTypes = [PortType.Text],
    };

    /// <summary>按类型专属声明建出类型实例，未声明时取默认声明。外部输入协议是建运行期对象所需的运行期依赖，由装配处给出。</summary>
    public static INodeType Create(InputParameters? parameters, IInputChannel channel) =>
        new InputNodeType(parameters, channel);
}
