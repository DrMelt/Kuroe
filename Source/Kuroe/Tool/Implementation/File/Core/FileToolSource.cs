namespace Kuroe.Tool.File;

/// <summary>文件工具源：装配期提供定义，运行期按执行绑定创建本轮可用的文件工具。</summary>
public sealed class FileToolSource : ToolSource
{
    private static readonly IReadOnlyList<ToolEntry> Entries =
    [
        new(ReadFileTool.Definition, binding => new ReadFileTool(binding)),
        new(ListDirectoryTool.Definition, binding => new ListDirectoryTool(binding)),
        new(GetFileInfoTool.Definition, binding => new GetFileInfoTool(binding)),
        new(WriteFileTool.Definition, binding => new WriteFileTool(binding)),
        new(PatchFileTool.Definition, binding => new PatchFileTool(binding)),
        new(MoveFileTool.Definition, binding => new MoveFileTool(binding)),
    ];

    /// <summary>建立文件工具源。</summary>
    public FileToolSource()
        : base(Entries)
    {
    }
}