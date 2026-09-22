using System.Text.Json.Nodes;
using ApiHub.Models;
using ErrorOr;
using Kuroe.Agent;
using Kuroe.Catalogs;
using Kuroe.Configuration;

namespace Kuroe.Cli.Commands;

/// <summary>/model 子命令的解析与执行。模型选择只经此命令，/set 与 /unset 拒绝模型路径。</summary>
internal sealed class ModelCommands(
    CatalogService catalog,
    SettingsProvider settings,
    Terminal terminal,
    ConsoleResults results,
    ConsoleErrors errors)
{
    /// <summary>取消选择的参数值。</summary>
    private const string NoneValue = "none";

    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/model", "打印当前模型"),
        ("/model <模型>", "切换模型，要求已注册"),
        ("/model none", "取消选择模型"),
        ("/model add <模型> <提供商>", "把模型注册到提供商"),
        ("/model rm <模型>", "注销模型"),
    ];

    private readonly CatalogService _catalog = catalog;
    private readonly SettingsProvider _settings = settings;
    private readonly Terminal _terminal = terminal;
    private readonly ConsoleResults _results = results;
    private readonly ConsoleErrors _errors = errors;

    public void Run(string[] parts)
    {
        const string usage = "用法：/model <模型> 切换，/model none 取消选择，/model add <模型> <提供商> 注册，/model rm <模型> 注销";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch ((subcommand, parts.Length))
        {
            case (_, 1):
                _terminal.Line($"当前模型 {CurrentModel}。");
                _terminal.Hint(usage);
                break;

            case (NoneValue, 2):
                Clear();
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

    private string CurrentModel => _settings.Current.Agent.Model?.Value ?? "未选择";

    /// <summary>模型必须已在目录中注册，否则写进偏好只让下一轮对话失败。</summary>
    private void Select(string modelName)
    {
        ErrorOr<ModelName> model = ModelName.Create(modelName);
        if (model.IsError)
        {
            _results.Reject(model.ErrorsOrEmptyList);
            return;
        }

        ErrorOr<ModelConnection> connection = _catalog.Connect(model.Value);
        if (connection.IsError)
        {
            _results.Reject(connection.ErrorsOrEmptyList);
            _errors.GuideModelRegistration(_catalog, model.Value);
            return;
        }

        _results.Apply(
            _settings,
            () => _settings.Set(AgentSettings.ModelPath, JsonValue.Create(model.Value.Value)!));
    }

    /// <summary>取消选择。用户层没有模型项时本来就未选择。</summary>
    private void Clear()
    {
        if (_settings.TryGetUserValue(AgentSettings.ModelPath) is null)
        {
            _terminal.Line($"当前模型 {CurrentModel}。");
            return;
        }

        _results.Apply(_settings, () => _settings.Clear(AgentSettings.ModelPath));
    }

    private void Add(string modelName, string providerName)
    {
        ErrorOr<ModelName> model = ModelName.Create(modelName);
        ErrorOr<ProviderName> provider = ProviderName.Create(providerName);

        List<Error> collected = [];
        ConsoleResults.Collect(model, collected);
        ConsoleResults.Collect(provider, collected);
        if (collected.Count > 0)
        {
            _results.Reject(collected);
            return;
        }

        _results.Report(_catalog.AddModel(model.Value, provider.Value), "已注册。");
    }

    /// <summary>注销模型，注销的是当前选择时一并取消选择。</summary>
    private void Remove(string modelName)
    {
        ErrorOr<ModelName> model = ModelName.Create(modelName);
        if (model.IsError)
        {
            _results.Reject(model.ErrorsOrEmptyList);
            return;
        }

        ErrorOr<Success> removed = _catalog.RemoveModel(model.Value);
        if (removed.IsError)
        {
            _results.Reject(removed.ErrorsOrEmptyList);
            return;
        }

        if (model.Value != _settings.Current.Agent.Model)
        {
            _results.Report(removed, "已注销。");
            return;
        }

        _terminal.Warn("已注销当前模型，选择一并取消。");
        _results.Apply(_settings, () => _settings.Clear(AgentSettings.ModelPath));
    }
}
