using ApiHub.Shared.Models;
using Kuroe.Node.Llm.Tools;
using Kuroe.Catalogs;
using Kuroe.Flow.Control;
using Kuroe.Flow.Persistence.Restoring.Shared;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Console.Views;

/// <summary>启动横幅：模型、目录与流程规模，以及缺少提供商、模型或工具时的处理指引。</summary>
internal sealed class StartupView(
    CatalogService catalog,
    ModelService models,
    ToolCollection tools,
    IFlowTemplateCatalog flows,
    Terminal terminal)
{
    public void Print(TaskLoadReport? report = null)
    {
        CatalogContents contents = catalog.Snapshot();
        string model = models.Current?.Value ?? "未选择";

        List<IRenderable> lines =
        [
            new Text($"Kuroe 已启动，当前模型 {model}。"),
            new Text($"目录中有 {contents.Providers.Length} 个提供商、{contents.Models.Length} 个模型，"
                + $"流程模板 {flows.All().Count} 条。", Styles.Hint),
        ];

        if (report is { } load && load.Busy.Count > 0)
        {
            lines.Add(new Text($"{load.Busy.Count} 个任务由其他进程持有，本实例未加载。", Styles.Hint));
        }

        if (contents.Providers.Length == 0)
        {
            lines.Add(new Text("先在「模型与提供商」菜单添加提供商，再注册模型。", Styles.Hint));
        }

        if (tools.Names.Count == 0)
        {
            lines.Add(new Text("当前没有可用工具，检查是否注册了工具载体、载体是否声明了函数。", Styles.Hint));
        }

        lines.Add(new Text("回车进入选中项，方向键切换菜单项，Esc 返回上一级。", Styles.Hint));

        terminal.Write(Blocks.Section(BoxKind.View, null, new Rows(lines)));
    }
}
