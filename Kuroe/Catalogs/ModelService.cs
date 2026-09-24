using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Configuration;
using Kuroe.Shared.Catalogs;
using Kuroe.Shared.Configuration;

namespace Kuroe.Catalogs;

/// <summary>当前模型的选择与注销。校验模型已在目录注册，并守住目录与选择的一致：注销当前模型即取消选择。</summary>
public sealed class ModelService(SettingsProvider settings, CatalogService catalog)
{
    private readonly SettingsProvider _settings = settings;
    private readonly CatalogService _catalog = catalog;

    /// <summary>当前选中的模型名，未选择时为空。</summary>
    public string? Current => _settings.Current.Agent.Model;

    /// <summary>模型是否已在目录中注册。</summary>
    public bool IsRegistered(string modelName) => _catalog.HasModel(modelName);

    /// <summary>选择模型，要求已注册。名称非法或未注册时返回错误，选择不改动。</summary>
    public ErrorOr<ModelSelection> Select(string modelName)
    {
        ErrorOr<ModelConnection> connection = _catalog.Connect(modelName);
        if (connection.IsError)
        {
            return connection.ErrorsOrEmptyList;
        }

        ErrorOr<SettingsEffect> written = _settings.SetModel(modelName);

        return written.IsError ? written.ErrorsOrEmptyList : new ModelSelection(true, written.Value);
    }

    /// <summary>取消选择。本来就未选择时 Changed 为假，用户层不变。</summary>
    public ErrorOr<ModelSelection> Unselect()
    {
        if (_settings.TryGetUserValue(AgentSettings.ModelPath) is null)
        {
            return new ModelSelection(false, SettingsEffect.None);
        }

        ErrorOr<SettingsEffect> cleared = _settings.SetModel(null);

        return cleared.IsError ? cleared.ErrorsOrEmptyList : new ModelSelection(true, cleared.Value);
    }

    /// <summary>从目录注销模型。注销的是当前选中模型时一并取消选择，两处改动分别落盘。</summary>
    public ErrorOr<ModelRemoval> Remove(string modelName)
    {
        ErrorOr<Success> removed = _catalog.RemoveModel(modelName);
        if (removed.IsError)
        {
            return removed.ErrorsOrEmptyList;
        }

        if (!string.Equals(modelName, Current, StringComparison.Ordinal))
        {
            return new ModelRemoval(SettingsEffect.None, []);
        }

        ErrorOr<SettingsEffect> cleared = _settings.SetModel(null);

        return cleared.IsError
            ? new ModelRemoval(SettingsEffect.None, cleared.ErrorsOrEmptyList)
            : new ModelRemoval(cleared.Value, []);
    }
}
