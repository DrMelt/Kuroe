using ApiHub.Models;
using ErrorOr;
using Kuroe.Catalogs;

namespace Kuroe.Cli;

/// <summary>错误到终端的统一输出与可恢复错误的操作指引。</summary>
internal static class Errors
{
    public static void Report(IEnumerable<Error> errors)
    {
        foreach (Error error in errors)
        {
            Console.Error.WriteLine($"错误：{error.Code}：{error.Description}");
        }
    }

    /// <summary>模型不在目录中时补一行注册命令，模型已注册时没有输出。</summary>
    public static void GuideModelRegistration(CatalogService catalog, ModelName model)
    {
        if (catalog.Connect(model).IsError)
        {
            Console.WriteLine($"用 /model add {model.Value} <提供商> 注册该模型。");
        }
    }
}
