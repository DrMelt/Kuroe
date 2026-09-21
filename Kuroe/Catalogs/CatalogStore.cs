using ApiHub.Json;
using ApiHub.Models;
using ErrorOr;
using Kuroe.Storage;

namespace Kuroe.Catalogs;

/// <summary>目录文件的读写，内容与 JSON 文本的转换交给 ApiHub。</summary>
public sealed class CatalogStore(string file)
{
    private readonly string _file = file;

    /// <summary>读取目录文件，文件不存在时得到空目录。</summary>
    public ErrorOr<CatalogContents> Load() =>
        File.Exists(_file) ? Read(_file) : CatalogContents.Create([], []);

    /// <summary>把内容写入目录文件。</summary>
    public ErrorOr<Success> Save(CatalogContents contents) => Write(_file, contents);

    /// <summary>从指定文件读取内容，供导入使用。</summary>
    public static ErrorOr<CatalogContents> Read(string path)
    {
        try
        {
            return CatalogJson.Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [CatalogErrors.Read(path, ex.Message)];
        }
    }

    /// <summary>把内容写入指定文件，供导出使用。</summary>
    public static ErrorOr<Success> Write(string path, CatalogContents contents)
    {
        try
        {
            AtomicFile.WriteText(path, contents.ToJson());

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [CatalogErrors.Write(path, ex.Message)];
        }
    }
}
