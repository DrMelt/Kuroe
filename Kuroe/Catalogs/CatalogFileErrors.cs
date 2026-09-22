using ErrorOr;

namespace Kuroe.Catalogs;

/// <summary>目录文件读写的错误构造。</summary>
static class CatalogFileErrors
{
    public static Error Read(string path, string message) =>
        Error.Failure("CatalogFile.Read", $"读取 {path} 失败：{message}");

    public static Error Write(string path, string message) =>
        Error.Failure("CatalogFile.Write", $"写入 {path} 失败：{message}");

    public static Error InvalidPath(string path, string message) =>
        Error.Validation("CatalogFile.InvalidPath", $"文件参数不是合法路径：{path}（{message}）");

    public static Error OverwritesCatalog(string path) =>
        Error.Validation("CatalogFile.OverwritesCatalog",
            $"{path} 是在用的目录文件，导出会抹掉其中全部凭据，换别的文件名。");
}