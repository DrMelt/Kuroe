using Kuroe.Files.Shared;

namespace Kuroe.Tool.File;

/// <summary>文件工具源：装配期提供定义，运行期按执行绑定创建本轮可用的文件工具。</summary>
/// <remarks>用文件访问实现建立工具源。</remarks>
public sealed class FileToolSource(IWorkspaceFiles files) : ToolSource(Entries(files))
{

    /// <summary>按文件访问实现建立全部文件工具的条目。</summary>
    private static IReadOnlyList<ToolEntry> Entries(IWorkspaceFiles files) =>
    [
        new(ReadFileTool.Definition, binding => new ReadFileTool(binding, files)),
        new(SearchTextTool.Definition, binding => new SearchTextTool(binding, files)),
        new(ListDirectoryTool.Definition, binding => new ListDirectoryTool(binding, files)),
        new(GetFileInfoTool.Definition, binding => new GetFileInfoTool(binding, files)),
        new(WriteFileTool.Definition, binding => new WriteFileTool(binding, files)),
        new(PatchFileTool.Definition, binding => new PatchFileTool(binding, files)),
        new(ReplaceTextTool.Definition, binding => new ReplaceTextTool(binding, files)),
        new(MoveFileTool.Definition, binding => new MoveFileTool(binding, files)),
        new(DeleteFileTool.Definition, binding => new DeleteFileTool(binding, files)),
    ];
}