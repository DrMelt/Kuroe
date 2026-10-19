namespace Kuroe.Tool.FileAccess;

/// <summary>文件的文件系统事实：字节大小、创建/修改/访问时间与属性位。不含内容层派生。</summary>
/// <param name="Size">文件字节数。</param>
/// <param name="Created">创建时间，本地时区。</param>
/// <param name="LastWrite">最后写入时间，本地时区。</param>
/// <param name="LastAccess">最后访问时间，本地时区。</param>
/// <param name="Attributes">文件属性位。</param>
public sealed record FileDetails(
    long Size,
    DateTimeOffset Created,
    DateTimeOffset LastWrite,
    DateTimeOffset LastAccess,
    FileAttributes Attributes);