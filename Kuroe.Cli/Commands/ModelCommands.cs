using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Cli.Views;
using Kuroe.Shared.Catalogs;

namespace Kuroe.Cli.Commands;

/// <summary>/model 子命令的解析与执行。模型选择只经此命令，注册校验与注销联动由库保证。</summary>
internal sealed class ModelCommands(
    CatalogService catalog,
    ModelService models,
    CatalogView printer,
    Terminal terminal,
    ResultView results,
    ErrorView errors)
{
    /// <summary>取消选择的参数值。</summary>
    private const string NoneValue = "none";

    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/model", "打印当前模型"),
        ("/model list", "列出已注册模型"),
        ("/model <模型>", "切换模型，要求已注册"),
        ("/model none", "取消选择模型"),
        ("/model add <模型> <提供商>", "把模型注册到提供商"),
        ("/model rm <模型>", "注销模型"),
    ];

    private readonly CatalogService _catalog = catalog;
    private readonly ModelService _models = models;
    private readonly CatalogView _printer = printer;
    private readonly Terminal _terminal = terminal;
    private readonly ResultView _results = results;
    private readonly ErrorView _errors = errors;

    public void Run(string[] parts)
    {
        const string usage = "用法：/model <模型> 切换，/model none 取消选择，/model list 列出已注册模型，/model add <模型> <提供商> 注册，/model rm <模型> 注销";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch ((subcommand, parts.Length))
        {
            case (_, 1):
                _terminal.Line($"当前模型 {CurrentModel}。");
                _terminal.Hint(usage);
                break;

            case (NoneValue, 2):
                Unselect();
                break;

            case ("list", 2):
                _printer.PrintModels(_catalog.Snapshot().Models);
                break;

            case (not ("add" or "rm"), 2):
                Select(parts[1]);
                break;

            case ("rm", 3):
                Remove(parts[2]);
                break;

            case ("add", 4):
                Add(parts[2], parts[3]);
                break;

            default:
                _terminal.Hint(usage);
                break;
        }
    }

    private string CurrentModel => _models.Current?.Value ?? "未选择";

    private void Select(string modelName)
    {
        ErrorOr<ModelName> parsed = ModelName.Create(modelName.Trim());
        if (parsed.IsError)
        {
            _results.Reject(parsed.ErrorsOrEmptyList);
            return;
        }

        ErrorOr<ModelSelection> selected = _models.Select(parsed.Value);
        if (selected.IsError)
        {
            _results.Reject(selected.ErrorsOrEmptyList);
            _errors.GuideModelRegistration(_models, modelName);
            return;
        }

        _results.Apply(selected.Value.Effect);
    }

    /// <summary>取消选择。用户层没有模型项时本来就未选择。</summary>
    private void Unselect()
    {
        ErrorOr<ModelSelection> unselected = _models.Unselect();
        if (unselected.IsError)
        {
            _results.Reject(unselected.ErrorsOrEmptyList);
            return;
        }

        if (!unselected.Value.Changed)
        {
            _terminal.Line($"当前模型 {CurrentModel}。");
            return;
        }

        _results.Apply(unselected.Value.Effect);
    }

    private void Add(string modelName, string providerName)
    {
        ErrorOr<ModelName> model = ModelName.Create(modelName.Trim());
        ErrorOr<ProviderName> provider = ProviderName.Create(providerName.Trim());
        if (model.IsError || provider.IsError)
        {
            _results.Reject([.. model.ErrorsOrEmptyList, .. provider.ErrorsOrEmptyList]);
            return;
        }

        _results.Report(_catalog.AddModel(model.Value, provider.Value), "已注册。");
    }

    private void Remove(string modelName)
    {
        ErrorOr<ModelName> parsed = ModelName.Create(modelName.Trim());
        if (parsed.IsError)
        {
            _results.Reject(parsed.ErrorsOrEmptyList);
            return;
        }

        bool wasSelected = _models.Current is { } current && parsed.Value.Equals(current);
        ErrorOr<ModelRemoval> removed = _models.Remove(parsed.Value);
        if (removed.IsError)
        {
            _results.Reject(removed.ErrorsOrEmptyList);
            return;
        }

        if (!wasSelected)
        {
            _terminal.Ok("已注销。");
            return;
        }

        if (removed.Value.Failures.Count > 0)
        {
            _terminal.Warn("已注销当前模型。");
            _errors.Report(removed.Value.Failures);
            _terminal.Note("取消选择未生效。");
            return;
        }

        _terminal.Warn("已注销当前模型，选择一并取消。");
        _results.Apply(removed.Value.Effect);
    }
}
