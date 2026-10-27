using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Process;

namespace Kuroe.Node.Command;

/// <summary>命令节点类型的对外入口：类型名、静态形状与实例建立。</summary>
public static class CommandNodeTypes
{
    /// <summary>类型名，与流程文件里 Kind 的取值对应。</summary>
    public static NodeKind Name { get; } = new("Command");

    /// <summary>未声明 TimeoutSeconds 时的等待上限秒数。</summary>
    public const int DefaultTimeoutSeconds = 120;

    /// <summary>未声明 MaxChars 时的输出上限。</summary>
    public const int DefaultMaxChars = 8000;

    /// <summary>命令节点的静态形状：路径变量给出执行目录，Signal 输入触发，输出承载命令输出。</summary>
    public static NodeTypeShape Shape { get; } = new()
    {
        InputTypes = [PortTypes.Text, PortTypes.Signal],
        OutputTypes = [PortTypes.Text],
    };

    /// <summary>按类型专属声明与进程执行边界建出类型实例。</summary>
    public static INodeType Create(CommandParameters parameters, IProcessRunner process) =>
        new CommandNodeType(parameters, process);
}