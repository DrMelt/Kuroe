using Kuroe.Files.Shared;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

/// <summary>文件工具源：装配期提供定义，运行期按执行绑定创建本轮可用的文件工具。</summary>
/// <remarks>用文件访问实现建立工具源。</remarks>
public sealed class FileToolSource(IWorkspaceFiles files) : ToolSource(Entries(files))
{

    /// <summary>按文件访问实现建立全部文件工具的条目。</summary>
    private static IReadOnlyList<ToolEntry> Entries(IWorkspaceFiles files) =>
    [
        new(ReadFileTool.Definition, binding => new ReadFileTool(FileTools.Scope(binding, files))),
        new(SearchTextTool.Definition, binding => SearchText(binding, files)),
        new(ListDirectoryTool.Definition, binding => new ListDirectoryTool(FileTools.Scope(binding, files))),
        new(GetFileInfoTool.Definition, binding => new GetFileInfoTool(FileTools.Scope(binding, files), files)),
        new(WriteFileTool.Definition, binding => new WriteFileTool(FileTools.Scope(binding, files), files)),
        new(ReplaceTextTool.Definition, binding => ReplaceText(binding, files)),
        new(MoveFileTool.Definition, binding => new MoveFileTool(FileTools.Scope(binding, files), files)),
        new(DeleteFileTool.Definition, binding => new DeleteFileTool(FileTools.Scope(binding, files), files)),
    ];

    /// <summary>用执行绑定的文件访问作用域建立文本搜索工具。</summary>
    private static SearchTextTool SearchText(ToolBinding binding, IWorkspaceFiles files)
    {
        FileScope scope = FileTools.Scope(binding, files);

        return new SearchTextTool(scope, new FileSearch(scope, FileTools.PathAccess));
    }

    /// <summary>用执行绑定的文件访问作用域建立批量文本替换工具。</summary>
    private static ReplaceTextTool ReplaceText(ToolBinding binding, IWorkspaceFiles files)
    {
        FileScope scope = FileTools.Scope(binding, files);

        return new ReplaceTextTool(scope, new FileReplacer(scope, FileTools.PathAccess));
    }
}