using System.Text;
using ApiHub.Shared.Models;
using Kuroe.Catalogs;
using Kuroe.Shared.Executions.Tools;

namespace Kuroe.Tools.KuroeTools;

/// <summary>模型目录的信息查询工具。只读，凭据不交给模型。</summary>
internal sealed class CatalogInfoTool : ITool
{
    private readonly CatalogService _catalog;
    private readonly ModelService _models;

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    public CatalogInfoTool(CatalogService catalog, ModelService models)
    {
        _catalog = catalog;
        _models = models;
        Functions =
        [
            new ToolFunction(new ToolName("GetCatalog"),
                "查看模型目录：提供商（端点与凭据是否已设置）、已注册模型与当前选中模型。",
                [], _ => Describe(), new ToolPath("info/GetCatalog")),
        ];
    }

    /// <summary>目录的文本概况。</summary>
    private string Describe()
    {
        CatalogContents contents = _catalog.Snapshot();
        var text = new StringBuilder();

        if (contents.Providers.Length == 0)
        {
            text.AppendLine("还没有提供商，用 /provider add <名> <端点> <凭据> 添加。");
        }
        else
        {
            text.AppendLine($"提供商（{contents.Providers.Length} 个）：");
            foreach (ProviderDefinition provider in contents.Providers)
            {
                string key = CatalogService.IsPlaceholder(provider.ApiKey) ? "凭据是占位符" : "凭据已设置";
                text.AppendLine($"  {provider.ProviderName.Value} · {provider.BaseAddress.Address} · {key}");
            }
        }

        if (contents.Models.Length == 0)
        {
            text.AppendLine("还没有注册模型，用 /model add <模型> <提供商> 注册。");
        }
        else
        {
            text.AppendLine($"模型（{contents.Models.Length} 个）：");
            foreach (ModelDefinition model in contents.Models)
            {
                text.AppendLine($"  {model.ModelName.Value} → {model.ProviderName.Value}");
            }
        }

        string current = string.IsNullOrEmpty(_models.Current) ? "未选择" : _models.Current;
        text.Append($"当前模型：{current}");

        return text.ToString().TrimEnd();
    }
}
