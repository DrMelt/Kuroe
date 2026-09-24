using ErrorOr;
using Kuroe;
using Kuroe.Catalogs;

namespace Kuroe.Cli.Views;

/// <summary>错误到终端的统一输出与可恢复错误的操作指引。</summary>
internal sealed class ErrorPrinter(Terminal terminal)
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
                case ErrorCodes.SettingsModelPath:
                    terminal.Hint("模型选择用 /model <模型> 切换，/model none 取消选择。");
                    break;

                case ErrorCodes.SettingsNullValue:
                    terminal.Hint("清除设置用 /unset <路径>。");
                    break;

                case ErrorCodes.TaskNotFound:
                    terminal.Hint("用 /task list 查看任务号。");
                    break;

                case ErrorCodes.RunNotFound:
                    terminal.Hint("agent 号全局递增，用 /task list 或 /task show <任务号> 查看。");
                    break;

                case ErrorCodes.WorkflowNotFound:
                    terminal.Hint("用 /flow list 查看流程名。");
                    break;

                case ErrorCodes.WorkflowInvalidPath:
                    terminal.Hint("/flow add 的文件参数相对工作目录解析，路径非法时换一个写法。");
                    break;

                case ErrorCodes.WorkflowStep or ErrorCodes.WorkflowBody or ErrorCodes.WorkflowName
                    or ErrorCodes.WorkflowFormat:
                    terminal.Hint("流程文件是工作目录下的 flows.json，改完用 /flow list 确认是否加载成功。");
                    break;
            }
        }
    }
}
