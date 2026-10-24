using Kuroe.Node.Llm.Tools;
using Kuroe.Tool.Json;

namespace Kuroe.Console.Views;

/// <summary>全部可用工具的只读树：按路径分段成层，叶行给出函数名、说明、参数与配置形态。</summary>
internal sealed class ToolView(
    ToolCollection tools,
    IReadOnlyList<ConfiguredTool> configuredTools,
    Terminal terminal)
{
    public void Print()
    {
        terminal.Page("主菜单", "工具");

        if (tools.Definitions.Count == 0)
        {
            terminal.Hint("没有可用工具，检查是否注册了工具载体、载体是否声明了函数。");
            return;
        }

        terminal.Write(Blocks.Section(
            BoxKind.View,
            $"工具 {tools.Definitions.Count} 个",
            ToolTree.Build(tools.Definitions, configuredTools)));
    }
}