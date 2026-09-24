using Kuroe.Agent.Tools;
using Kuroe.Catalogs;
using Kuroe.Workflows.Flows;

namespace Kuroe.Cli.Views;

/// <summary>启动横幅：模型、目录与流程规模，以及缺少提供商、模型或工具时的处理指引。</summary>
internal sealed class StartupView(
    CatalogService catalog,
    ModelService models,
    ToolCollection tools,
    WorkflowService flows,
    Terminal terminal)
{
    public void Print()
    {
        CatalogSnapshot contents = catalog.Snapshot();
        string model = models.Current ?? "未选择";
        terminal.Line($"Kuroe 已启动，当前模型 {model}，" +
            $"目录中有 {contents.Providers.Count} 个提供商、{contents.Models.Count} 个模型，" +
            $"流程模板 {flows.All().Count} 条（默认 {flows.DefaultName}）。");
        if (contents.Providers.Count == 0)
        {
            terminal.Hint("先 /provider add <提供商> <端点> <凭据> 添加提供商，再 /model add <模型> <提供商> 注册模型。");
        }

        if (tools.Names.Count == 0)
        {
            terminal.Hint("当前没有可用工具，检查是否注册了工具载体、载体是否声明了函数。");
        }

        if (flows.Find(flows.DefaultName).IsError)
        {
            terminal.Hint($"默认流程 {flows.DefaultName} 不可用，用 /flow default <流程> 指定或 /flow add <文件> 导入。");
        }

        terminal.Hint("普通输入在当前任务里对话；/task new <目标> 提交任务，按流程派 agent。");
        terminal.Hint("/task 打开浏览器，任务 → agent → 详情逐级进入；exit 退出，/help 查看命令，Ctrl+C 中断当前回复。");
    }
}
