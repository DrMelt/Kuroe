using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Cli.Views;

namespace Kuroe.Cli.Commands;

/// <summary>/model 子命令的解析与执行。模型选择只经此命令，注册校验与注销联动由库保证。</summary>
internal sealed class ModelCommands(
    CatalogService catalog,
    ModelService models,
    CatalogPrinter printer,
    Terminal terminal,
    ResultPrinter results,
    ErrorPrinter errors)
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
    private readonly CatalogPrinter _printer = printer;
    private readonly Terminal _terminal = terminal;
    private readonly ResultPrinter _results = results;
    private readonly ErrorPrinter _errors = errors;

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

    private string CurrentModel => _models.Current ?? "未选择";

    private void Select(string modelName)
    {
        ErrorOr<ModelSelection> selected = _models.Select(modelName);
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

    private void Add(string modelName, string providerName) =>
        _results.Report(_catalog.AddModel(modelName, providerName), "已注册。");

    private void Remove(string modelName)
    {
        bool wasSelected = string.Equals(modelName, _models.Current, StringComparison.Ordinal);
        ErrorOr<ModelRemoval> removed = _models.Remove(modelName);
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
