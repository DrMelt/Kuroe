using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Node.File;

/// <summary>文件写入节点类型的对外入口：类型名、静态形状与实例建立。按路径变量解析路径，把唯一的内容 Text 输入写入文件。</summary>
public static class FileWriteNodeTypes
{
    /// <summary>类型名，与流程文件里 Kind 的取值对应。</summary>
    public static NodeKind Name { get; } = new("FileWrite");

    /// <summary>未声明 MaxChars 时的写入上限，0 表示不限制。</summary>
    public const int DefaultMaxChars = 0;

    /// <summary>文件写入节点的静态形状：Text 输入承载写入内容或路径变量，Signal 输入只作触发，输出可选。</summary>
    public static NodeTypeShape Shape { get; } = new()
    {
        InputTypes = [PortTypes.Text, PortTypes.Signal],
        OutputTypes = [PortTypes.Text, PortTypes.Signal],
    };

    /// <summary>按类型专属声明建出类型实例。</summary>
    public static INodeType Create(FileParameters parameters) => new FileWriteNodeType(parameters);
}