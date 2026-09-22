using ErrorOr;
using Kuroe.Agent;

namespace Kuroe.Cli;

/// <summary>错误到终端的统一输出与可恢复错误的操作指引。</summary>
internal sealed class ConsoleErrors(Terminal terminal)
{
    public void Report(IEnumerable<Error> errors)
    {
        foreach (Error error in errors)
        {
            terminal.Error($"错误：{error.Code}：{error.Description}");
        }
    }

    /// <summary>模型不可用时给出可用操作：未选择时给出登记流程，名称合法但未注册时给出该模型的注册命令。
    /// 空或全空白的名称不给指引，按这样的名字注册无从下手。</summary>
    public void GuideModelRegistration(ModelService models, string? model)
    {
        if (model is null)
        {
            terminal.Hint("用 /model <模型> 选择模型，没有已注册的模型时先 /model add <模型> <提供商> 注册。");
            return;
        }

        if (!string.IsNullOrWhiteSpace(model) && !models.IsRegistered(model))
        {
            terminal.Hint($"用 /model add {model} <提供商> 注册该模型。");
        }
    }

    /// <summary>按错误码补充可操作提示，没有对应提示时没有输出。</summary>
    public void Guide(IEnumerable<Error> errors)
    {
        foreach (Error error in errors)
        {
            switch (error.Code)
            {
                case "Settings.ModelPath":
                    terminal.Hint("模型选择用 /model <模型> 切换，/model none 取消选择。");
                    break;

                case "Settings.NullValue":
                    terminal.Hint("清除设置用 /unset <路径>。");
                    break;
            }
        }
    }
}
