using System.Text;
using ErrorOr;

namespace Kuroe.Tool.Info;

/// <summary>模型目录的信息查询工具。只读，凭据不交给模型。</summary>
public sealed class CatalogInfoTool : ITool
{
    private readonly ICatalogView _catalog;

    /// <summary>查看模型目录的函数定义。</summary>
    private static readonly ToolDefinition GetCatalog = new(
        new ToolPath("info/GetCatalog"),
        "查看模型目录：提供商（端点与凭据是否已设置）、已注册模型与当前选中模型。",
        []);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用模型目录的只读视图建立载体。</summary>
    public CatalogInfoTool(ICatalogView catalog)
    {
        _catalog = catalog;
        Functions = [GetCatalog.Bind(_ => Describe())];
    }

    /// <summary>目录的文本概况。</summary>
    private ErrorOr<string> Describe()
    {
        CatalogSummary summary = _catalog.Read();
        IReadOnlyList<ProviderInfo> providers = summary.Providers;
        IReadOnlyList<ModelInfo> models = summary.Models;
        var text = new StringBuilder();

        if (providers.Count == 0)
        {
            text.AppendLine("还没有提供商，用 /provider add <名> <端点> <凭据> 添加。");
        }
        else
        {
            text.AppendLine($"提供商（{providers.Count} 个）：");
            foreach (ProviderInfo provider in providers)
            {
                string key = provider.CredentialSet ? "凭据已设置" : "凭据是占位符";
                text.AppendLine($"  {provider.Name} · {provider.Endpoint} · {key}");
            }
        }

        if (models.Count == 0)
        {
            text.AppendLine("还没有注册模型，用 /model add <模型> <提供商> 注册。");
        }
        else
        {
            text.AppendLine($"模型（{models.Count} 个）：");
            foreach (ModelInfo model in models)
            {
                text.AppendLine($"  {model.Name} → {model.Provider}");
            }
        }

        string current = summary.CurrentModel ?? "未选择";
        text.Append($"当前模型：{current}");

        return text.ToString().TrimEnd();
    }
}
