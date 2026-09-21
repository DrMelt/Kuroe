using ApiHub.Models;
using ErrorOr;

namespace Kuroe.Catalogs;

/// <summary>目录域的错误构造。</summary>
public static class CatalogErrors
{
    public static Error ModelNotRegistered(ModelName modelName) => Error.NotFound(
        "Catalog.ModelNotRegistered",
        $"目录中没有模型 {modelName.Value}。");

    public static Error ProviderNotFound(ProviderName providerName) => Error.NotFound(
        "Catalog.ProviderNotFound",
        $"提供商 {providerName.Value} 不存在。");

    public static Error Read(string path, string message) =>
        Error.Failure("Catalog.Read", $"读取 {path} 失败：{message}");

    public static Error Write(string path, string message) =>
        Error.Failure("Catalog.Write", $"写入 {path} 失败：{message}");
}