using System.Text;
using ErrorOr;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

/// <summary>文件访问工具面共用的写入上限、工作目录边界与写回编码解析。</summary>
internal static class FileTools
{
    /// <summary>一次写入的字符上限，超出整段拒绝。</summary>
    internal const int ContentLimit = 40000;

    /// <summary>按工作目录建立路径解析的根边界。</summary>
    internal static FileScope Scope(string baseDirectory) => new(baseDirectory, "工作目录");

    /// <summary>解析写回编码。缺省或空白为 UTF-8 无 BOM，只支持 UTF 族编码，未知值给出带实参的详细错误。</summary>
    internal static ErrorOr<Encoding> ParseEncoding(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? new UTF8Encoding(false)
            : name.Trim().ToLowerInvariant() switch
            {
                "utf-8" => new UTF8Encoding(false),
                "utf-8-bom" => new UTF8Encoding(true),
                "utf-16" => Encoding.Unicode,
                "utf-16be" => Encoding.BigEndianUnicode,
                _ => ToolErrors.Argument($"encoding 只支持 utf-8、utf-8-bom、utf-16、utf-16be，收到 {name}。"),
            };
}