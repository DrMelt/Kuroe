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

    /// <summary>当前模型不可用时给出可用操作，模型可用时没有输出。</summary>
    public static void GuideModelRegistration(CatalogService catalog, ModelName? model)
    {
        if (model is null)
        {
            Console.WriteLine("用 /model <模型> 选择模型，没有已注册的模型时先 /model add <模型> <提供商> 注册。");
            return;
        }

        if (catalog.Connect(model).IsError)
        {
            Console.WriteLine($"用 /model add {model.Value} <提供商> 注册该模型。");
        }
    }
}
