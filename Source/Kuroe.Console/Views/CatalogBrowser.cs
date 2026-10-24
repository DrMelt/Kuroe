using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Shared.Catalogs;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>模型与提供商菜单：目录查看、提供商与模型维护、目录导入导出。</summary>
internal sealed class CatalogBrowser(
    CatalogService catalog,
    ModelService models,
    CatalogView printer,
    Terminal terminal,
    ResultView results,
    ErrorView errors)
{
    /// <summary>一个菜单项。</summary>
    private sealed record Item(string Label, string Action);

    private static readonly Item Back = new("← 返回", "back");

    public void Browse()
    {
        using (terminal.Exclusive())
        {
            while (true)
            {
                terminal.Page("主菜单", "模型与提供商");
                terminal.Line($"当前模型 {CurrentModel}。");
                terminal.NewLine();

                Item[] items =
                [
                    new("≡ 查看目录", "list"),
                    new("+ 添加提供商", "addProvider"),
                    new("* 更换提供商凭据", "setProviderKey"),
                    new("- 删除提供商", "removeProvider"),
                    new("+ 注册模型", "addModel"),
                    new("√ 选择模型", "selectModel"),
                    new("- 注销模型", "removeModel"),
                    new("↓ 导出目录", "export"),
                    new("↑ 导入目录", "import"),
                    Back,
                ];

                Item picked = terminal.Choose("目录操作", items, item => item.Label, Back);
                if (picked.Action == "back")
                {
                    return;
                }

                switch (picked.Action)
                {
                    case "list":
                        ViewPage.Browse(terminal, "目录", PrintCatalog);
                        break;

                    case "addProvider":
                        AddProvider();
                        break;

                    case "setProviderKey":
                        SetProviderKey();
                        break;

                    case "removeProvider":
                        RemoveProvider();
                        break;

                    case "addModel":
                        AddModel();
                        break;

                    case "selectModel":
                        PickModel();
                        break;

                    case "removeModel":
                        RemoveModel();
                        break;

                    case "export":
                        Export();
                        break;

                    case "import":
                        Import();
                        break;
                }
            }
        }
    }

    /// <summary>列出提供商与模型。</summary>
    public void PrintCatalog()
    {
        terminal.Page("主菜单", "模型与提供商", "目录");
        CatalogContents contents = catalog.Snapshot();
        printer.PrintProviders(contents.Providers);
        terminal.NewLine();
        printer.PrintModels(contents.Models, models.Current);
    }

    /// <summary>新增提供商。</summary>
    public void AddProvider(string name, string baseAddress, string credential) =>
        results.Report(catalog.AddProvider(name, baseAddress, credential), "已保存。");

    /// <summary>更换提供商凭据。</summary>
    public void SetProviderKey(ProviderDefinition provider, string credential) =>
        results.Report(catalog.SetProviderKey(provider.ProviderName.Value, credential), "已保存。");

    /// <summary>删除提供商，仍被模型引用时由目录拒绝。</summary>
    public void RemoveProvider(ProviderDefinition provider) =>
        results.Report(catalog.RemoveProvider(provider.ProviderName.Value), "已删除。");

    private string CurrentModel => models.Current?.Value ?? "未选择";

    /// <summary>把模型注册到提供商。</summary>
    public void AddModel(ModelName model, ProviderName provider) =>
        results.Report(catalog.AddModel(model, provider), "已注册。");

    /// <summary>选择已注册的模型。</summary>
    public void SelectModel(ModelName model)
    {
        ErrorOr<ModelSelection> selected = models.Select(model);
        if (selected.IsError)
        {
            results.Reject(selected.ErrorsOrEmptyList);
            errors.GuideModelRegistration(models, model.Value);
            return;
        }

        results.Apply(selected.Value.Effect);
    }

    /// <summary>取消选择。用户层没有模型项时本来就未选择。</summary>
    public void UnselectModel()
    {
        ErrorOr<ModelSelection> unselected = models.Unselect();
        if (unselected.IsError)
        {
            results.Reject(unselected.ErrorsOrEmptyList);
            return;
        }

        if (!unselected.Value.Changed)
        {
            terminal.Line($"当前模型 {CurrentModel}。");
            return;
        }

        results.Apply(unselected.Value.Effect);
    }

    /// <summary>注销模型，注销的是当前模型时选择一并取消。</summary>
    public void RemoveModel(ModelName model)
    {
        bool wasSelected = models.Current is { } current && model.Equals(current);
        ErrorOr<ModelRemoval> removed = models.Remove(model);
        if (removed.IsError)
        {
            results.Reject(removed.ErrorsOrEmptyList);
            return;
        }

        if (!wasSelected)
        {
            terminal.Ok("已注销。");
            return;
        }

        if (removed.Value.Failures.Count > 0)
        {
            terminal.Warn("已注销当前模型。");
            errors.Report(removed.Value.Failures);
            terminal.Note("取消选择未生效。");
            return;
        }

        terminal.Warn("已注销当前模型，选择一并取消。");
        results.Apply(removed.Value.Effect);
    }

    /// <summary>导出脱敏目录。</summary>
    public void Export(string file)
    {
        ErrorOr<string> exported = catalog.Export(file);
        if (exported.IsError)
        {
            results.Reject(exported.ErrorsOrEmptyList);
            return;
        }

        terminal.Ok($"已导出到 {exported.Value}，凭据已替换为占位符。");
    }

    /// <summary>合并导入目录，列出跳过的条目与仍是占位符的凭据。</summary>
    public void Import(string file)
    {
        ErrorOr<CatalogMerge> imported = catalog.Import(file);
        if (imported.IsError)
        {
            results.Reject(imported.ErrorsOrEmptyList);
            return;
        }

        terminal.Ok($"已从 {imported.Value.Source} 合并导入。");
        foreach (string note in imported.Value.Notes)
        {
            terminal.Hint($"  {note}");
        }

        IReadOnlyList<string> masked = imported.Value.PlaceholderProviders;
        if (masked.Count > 0)
        {
            terminal.Hint($"用「更换提供商凭据」替换占位符凭据：{string.Join('、', masked)}");
        }
    }

    private void AddProvider()
    {
        string? name = terminal.Ask("提供商名称：");
        if (name is null)
        {
            return;
        }

        string? endpoint = terminal.Ask("端点：");
        if (endpoint is null)
        {
            return;
        }

        string? credential = terminal.AskSecret("凭据：");
        if (credential is not null)
        {
            AddProvider(name, endpoint, credential);
        }
    }

    private void SetProviderKey()
    {
        if (PickProvider() is not { } provider)
        {
            return;
        }

        string? credential = terminal.AskSecret($"新凭据，提供商 {Markup.Escape(provider.ProviderName.Value)}：");
        if (credential is not null)
        {
            SetProviderKey(provider, credential);
        }
    }

    private void RemoveProvider()
    {
        if (PickProvider() is not { } provider)
        {
            return;
        }

        if (Confirm($"删除提供商 {provider.ProviderName.Value}？"))
        {
            RemoveProvider(provider);
        }
    }

    private void AddModel()
    {
        string? name = terminal.Ask("模型名：");
        if (name is null)
        {
            return;
        }

        ErrorOr<ModelName> parsed = ModelName.Create(name.Trim());
        if (parsed.IsError)
        {
            results.Reject(parsed.ErrorsOrEmptyList);
            return;
        }

        if (PickProvider() is { } provider)
        {
            AddModel(parsed.Value, provider.ProviderName);
        }
    }

    /// <summary>选择模型，含取消选择。模型选择由目录维护，助手页复用同一入口。</summary>
    public void PickModel()
    {
        IReadOnlyList<ModelDefinition> all = catalog.Snapshot().Models;
        if (all.Count == 0)
        {
            terminal.Hint("还没有注册模型，先注册模型。");
            return;
        }

        List<string> labels = [.. all.Select(ModelLabel), "取消选择模型"];
        if (Choices.Pick(terminal, "选择模型", labels) is not { } index)
        {
            return;
        }

        if (index == all.Count)
        {
            UnselectModel();
            return;
        }

        SelectModel(all[index].ModelName);
    }

    private void RemoveModel()
    {
        IReadOnlyList<ModelDefinition> all = catalog.Snapshot().Models;
        if (all.Count == 0)
        {
            terminal.Hint("还没有注册模型。");
            return;
        }

        if (Choices.Pick(terminal, "选择模型", [.. all.Select(ModelLabel)]) is not { } index)
        {
            return;
        }

        ModelDefinition model = all[index];
        if (Confirm($"注销模型 {model.ModelName.Value}？"))
        {
            RemoveModel(model.ModelName);
        }
    }

    private void Export()
    {
        string? file = terminal.Ask("导出文件：");
        if (file is not null)
        {
            Export(file);
        }
    }

    private void Import()
    {
        string? file = terminal.Ask("导入文件：");
        if (file is not null)
        {
            Import(file);
        }
    }

    private ProviderDefinition? PickProvider()
    {
        IReadOnlyList<ProviderDefinition> providers = catalog.Snapshot().Providers;
        if (providers.Count == 0)
        {
            terminal.Hint("目录中还没有提供商，先添加提供商。");
            return null;
        }

        int? index = Choices.Pick(terminal, "选择提供商",
            [.. providers.Select(provider => $"{provider.ProviderName.Value} · {provider.BaseAddress.Address}")]);

        return index is { } at ? providers[at] : null;
    }

    private string ModelLabel(ModelDefinition model)
    {
        bool selected = models.Current is { } current && model.ModelName.Equals(current);
        string label = $"{model.ModelName.Value} · {model.ProviderName.Value}";

        return selected ? $"{label} · 当前" : label;
    }

    private bool Confirm(string title) =>
        Confirmation.Ask(terminal, title, "√ 确认", "← 返回，不执行");
}