using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Node.File;

/// <summary>文件读取节点类型的对外入口：类型名、静态形状与实例建立。按 BasePath 解析路径并读取文件，写入唯一的输出端口。</summary>
public static class FileReadNodeTypes
{
    /// <summary>类型名，与流程文件里 Kind 的取值对应。</summary>
    public static NodeKind Name { get; } = new("FileRead");

    /// <summary>未声明 MaxChars 时的读取上限。</summary>
    public const int DefaultMaxChars = 8000;

    /// <summary>文件读取节点的静态形状：BasePath 给出解析基准，Signal 输入只作触发，输出承载读取内容。</summary>
    public static NodeTypeShape Shape { get; } = new()
    {
        InputTypes = [PortTypes.Text, PortTypes.Signal],
        OutputTypes = [PortTypes.Text],
    };

    /// <summary>按类型专属声明建出类型实例。</summary>
    public static INodeType Create(FileParameters parameters) => new FileReadNodeType(parameters);
}