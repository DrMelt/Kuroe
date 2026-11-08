using System.Text;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.FileAccess.Output;

/// <summary>目录列表文本：展示路径、条目名与目录标记，超出条目上限时截断并提示。</summary>
/// <param name="DisplayPath">目录的展示路径。</param>
/// <param name="Entries">目录条目，按名字序数排序。</param>
public readonly record struct DirectoryListingText(
    string DisplayPath,
    IReadOnlyList<DirectoryEntry> Entries)
{
    /// <summary>目录列表的条目上限。</summary>
    public const int MaxEntries = 200;

    /// <summary>目录列表文本。空目录只返回提示。</summary>
    public override string ToString()
    {
        if (Entries.Count == 0)
        {
            return $"目录 {DisplayPath} 为空。";
        }

        var text = new StringBuilder();
        text.Append($"{DisplayPath} 的条目（{Entries.Count}）：");
        foreach (DirectoryEntry entry in Entries.Take(MaxEntries))
        {
            text.Append($"\n  {entry.Name}{(entry.IsDirectory ? "/" : string.Empty)}");
        }

        if (Entries.Count > MaxEntries)
        {
            text.Append($"\n…仅列出前 {MaxEntries} 条");
        }

        return text.ToString();
    }
}