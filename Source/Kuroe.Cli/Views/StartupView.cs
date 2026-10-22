using ApiHub.Shared.Models;
using Kuroe.Node.Llm.Tools;
using Kuroe.Catalogs;
using Kuroe.Workflows.Flows;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Cli.Views;

/// <summary>启动横幅：模型、目录与流程规模，以及缺少提供商、模型或工具时的处理指引。</summary>
internal sealed class StartupView(
    CatalogService catalog,
    ModelService models,
    ToolCollection tools,
    FlowService flows,
    Terminal terminal)
{
    public void Print()
    {
        CatalogContents contents = catalog.Snapshot();
        string model = models.Current?.Value ?? "未选择";

        List<IRenderable> lines =
        [
            new Text($"已启动，当前模型 {model}。"),
            new Text($"目录中有 {contents.Providers.Length} 个提供商、{contents.Models.Length} 个模型，"
                + $"流程模板 {flows.All().Count} 条。", Styles.Hint),
        ];

        if (contents.Providers.Length == 0)
        {
            lines.Add(new Text("先 /provider add <提供商> <端点> <凭据> 添加提供商，再 /model add <模型> <提供商> 注册模型。", Styles.Hint));
        }

        if (tools.Names.Count == 0)
        {
            lines.Add(new Text("当前没有可用工具，检查是否注册了工具载体、载体是否声明了函数。", Styles.Hint));
        }

        lines.Add(new Text("普通输入与常驻对话任务对话；/task new --flow <流程> <目标> 提交任务，按流程启动执行。", Styles.Hint));
        lines.Add(new Text("/task 打开任务浏览器，任务 → 执行 → 详情逐级进入；/exit 退出，/help 查看命令。", Styles.Hint));

        terminal.Write(Blocks.Section(BoxKind.View, null, new Rows(lines)));
    }
}
