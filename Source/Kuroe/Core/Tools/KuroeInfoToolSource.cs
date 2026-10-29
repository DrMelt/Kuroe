using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Flow;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Shared.Catalogs;
using Kuroe.Shared.Configuration;
using Kuroe.Tool;
using Kuroe.Tool.KuroeInfo;

namespace Kuroe.Tools;

/// <summary>核心状态查询工具源：任务、目录、流程与设置四组查询工具。任一定义放行时创建全部载体。</summary>
internal sealed class KuroeInfoToolSource(
    TaskRegistry tasks,
    ICatalogView catalog,
    IFlowCatalog flows,
    ISettingsView settings) : IToolSource
{
    /// <inheritdoc/>
    public IReadOnlyList<ToolDefinition> Definitions { get; } =
    [
        .. new TaskKuroeInfoTool(tasks).Functions.Select(function => function.Definition),
        .. new CatalogKuroeInfoTool(catalog).Functions.Select(function => function.Definition),
        .. new FlowKuroeInfoTool(flows).Functions.Select(function => function.Definition),
        .. new SettingsKuroeInfoTool(settings).Functions.Select(function => function.Definition),
    ];

    /// <inheritdoc/>
    public IReadOnlyList<ITool> Create(ToolContext context)
    {
        List<ITool> tools = [];
        Add(new TaskKuroeInfoTool(tasks));
        Add(new CatalogKuroeInfoTool(catalog));
        Add(new FlowKuroeInfoTool(flows));
        Add(new SettingsKuroeInfoTool(settings));

        return tools;

        void Add(ITool tool)
        {
            if (context.AllowsAny(tool))
            {
                tools.Add(tool);
            }
        }
    }
}