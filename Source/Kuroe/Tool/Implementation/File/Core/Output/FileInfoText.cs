using Kuroe.Files.Shared;

namespace Kuroe.Tool.File.Output;

/// <summary>文件信息文本：路径、大小、时间与属性。</summary>
/// <param name="Path">文件的展示路径。</param>
/// <param name="Facts">文件系统事实。</param>
public readonly record struct FileInfoText(string Path, FileFacts Facts)
{
    /// <summary>文件信息文本，每项一行。</summary>
    public override string ToString() =>
        string.Join('\n',
            Path,
            $"大小：{Facts.Size} 字节",
            $"创建时间：{Facts.Created:yyyy-MM-dd HH:mm:ss}",
            $"修改时间：{Facts.LastWrite:yyyy-MM-dd HH:mm:ss}",
            $"访问时间：{Facts.LastAccess:yyyy-MM-dd HH:mm:ss}",
            $"属性：{Describe(Facts.Attributes)}");

    /// <summary>属性位里的四项映射成中文名并以顿号连接，四项都为假时为「无」。</summary>
    private static string Describe(FileAttributes attributes)
    {
        List<string> names = [];
        if (attributes.HasFlag(FileAttributes.ReadOnly))
        {
            names.Add("只读");
        }

        if (attributes.HasFlag(FileAttributes.Hidden))
        {
            names.Add("隐藏");
        }

        if (attributes.HasFlag(FileAttributes.System))
        {
            names.Add("系统");
        }

        if (attributes.HasFlag(FileAttributes.Archive))
        {
            names.Add("归档");
        }

        return names.Count == 0 ? "无" : string.Join('、', names);
    }
}