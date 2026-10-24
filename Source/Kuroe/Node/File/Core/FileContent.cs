using System.Text;
using IOFile = System.IO.File;

namespace Kuroe.Node.File;

/// <summary>文件节点的读写：读取按字符上限截断，写入按字符上限拒绝并建立缺失的父目录。</summary>
internal static class FileContent
{
    /// <summary>写回编码，UTF-8 不带 BOM。</summary>
    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>读取文件，超出字符上限时截断并加标记。上限为 0 时不限制。</summary>
    internal static string Read(string fullPath, int maxChars)
    {
        if (maxChars is <= 0 or int.MaxValue)
        {
            return IOFile.ReadAllText(fullPath);
        }

        using StreamReader reader = new(fullPath);
        char[] buffer = new char[maxChars + 1];
        int length = 0;
        while (length < buffer.Length)
        {
            int read = reader.Read(buffer, length, buffer.Length - length);
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        return length <= maxChars
            ? new string(buffer, 0, length)
            : string.Concat(buffer.AsSpan(0, maxChars), "…内容已截断");
    }

    /// <summary>覆盖写入整个文件，父目录缺失时建立。内容超出字符上限时不写入并返回原因，上限为 0 时不限制。</summary>
    internal static string? Write(string fullPath, string content, int maxChars)
    {
        if (maxChars > 0 && content.Length > maxChars)
        {
            return $"写入内容超过上限 {maxChars} 字符，本次写入不执行。";
        }

        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        IOFile.WriteAllText(fullPath, content, Utf8);

        return null;
    }
}