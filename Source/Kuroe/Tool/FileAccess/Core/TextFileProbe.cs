using Kuroe.Files.Shared;

namespace Kuroe.Tool.FileAccess;

/// <summary>文本文件探测：读取文件开头的一段字节，按 BOM 与 NUL 字节识别文本格式。</summary>
public static class TextFileProbe
{
    /// <summary>二进制判定的探测字节数。</summary>
    public const int ProbeSize = 8192;

    /// <summary>读取文件开头的一段字节并识别文本格式。没有已知 BOM 且含 NUL 时返回 null，表示二进制文件。</summary>
    public static TextFileFormat? Detect(IWorkspaceFiles files, string file)
    {
        using Stream stream = files.OpenRead(file);
        byte[] head = new byte[ProbeSize];
        int read = 0;
        while (read < head.Length)
        {
            int count = stream.Read(head, read, head.Length - read);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        return TextFileFormat.Detect(head.AsSpan(0, read));
    }

    /// <summary>按前 8 KB 的 BOM 与 NUL 字节判断是否为文本文件。带 UTF 族 BOM 的文件按文本处理。</summary>
    public static bool IsText(IWorkspaceFiles files, string file) => Detect(files, file) is not null;
}