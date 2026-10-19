using ErrorOr;

namespace Kuroe.Tool.Json;

/// <summary>工具文件的形状：文件名与从配置目录到配置工具的读入。承载工具的工具面各由一个 Json 项目实现本接口，
/// JSON 的读写由文件层承担，文件名到实现的对应只在这里。</summary>
public interface IToolFile
{
    /// <summary>本文件层的文件名，位于配置目录下。</summary>
    string FileName { get; }

    /// <summary>读文件产出配置工具，文件不存在时为空，内容不合法时一次给出全部错误。</summary>
    /// <param name="configDirectory">配置目录，文件按文件名在其中查找。</param>
    /// <param name="workDirectory">工作目录，相对路径以此为基准。</param>
    ErrorOr<IReadOnlyList<ConfiguredTool>> Load(string configDirectory, string workDirectory);
}