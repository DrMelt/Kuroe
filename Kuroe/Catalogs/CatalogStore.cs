using ApiHub.Json;
using ApiHub.Models;
using ErrorOr;

namespace Kuroe.Catalogs;

/// <summary>目录文件的读写。写盘先写临时文件再替换，中断不会留下半份文件。</summary>
public sealed class CatalogStore
{
    private const string ReadError = "Catalog.Read";
    private const string WriteError = "Catalog.Write";

    private readonly string _file;

    public CatalogStore(string file) => _file = file;

    /// <summary>读取目录文件，文件不存在时得到空目录。</summary>
    public ErrorOr<CatalogContents> Load() =>
        File.Exists(_file) ? Read(_file) : CatalogContents.Create([], []);

    /// <summary>把内容写入目录文件。</summary>
    public ErrorOr<Success> Save(CatalogContents contents) => Write(_file, contents);

    /// <summary>从指定文件读取内容，供导入使用。</summary>
    public ErrorOr<CatalogContents> Read(string path)
    {
        try
        {
            return CatalogJson.Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [Error.Failure(ReadError, $"读取 {path} 失败：{ex.Message}")];
        }
    }

    /// <summary>把内容写入指定文件，供导出使用。</summary>
    public ErrorOr<Success> Write(string path, CatalogContents contents)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

            string temporary = path + ".tmp";
            File.WriteAllText(temporary, contents.ToJson());
            File.Move(temporary, path, overwrite: true);

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [Error.Failure(WriteError, $"写入 {path} 失败：{ex.Message}")];
        }
    }
}
