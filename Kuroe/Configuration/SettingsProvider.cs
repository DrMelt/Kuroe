using System.Reflection;
using System.Text.Json.Nodes;
using ErrorOr;

namespace Kuroe.Configuration;

/// <summary>偏好配置的唯一生效点：绑定用户层节点树、校验改动、替换当前快照。凭据不在这里，见 <see cref="Catalogs.CatalogService"/>。</summary>
public sealed class SettingsProvider
{
    private readonly KuroePaths _paths;
    private readonly UserSettingsStore _store;

    private SettingsProvider(KuroePaths paths, UserSettingsStore store, KuroeSettings current)
    {
        _paths = paths;
        _store = store;
        Current = current;
    }

    /// <summary>当前生效的配置。</summary>
    public KuroeSettings Current { get; private set; }

    public string UserSettingsFile => _paths.UserSettingsFile;

    /// <summary>绑定用户层并校验，失败时一次给出全部错误。</summary>
    public static ErrorOr<SettingsProvider> Create(KuroePaths paths)
    {
        UserSettingsStore store;
        try
        {
            store = new UserSettingsStore(paths.UserSettingsFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return [Error.Failure("Settings.UserFile", $"读取 {paths.UserSettingsFile} 失败：{ex.Message}")];
        }

        ErrorOr<KuroeSettings> bound = KuroeSettings.From(store.Snapshot());

        return bound.IsError ? bound.ErrorsOrEmptyList : new SettingsProvider(paths, store, bound.Value);
    }

    /// <summary>改动用户层并立即生效。校验失败时返回错误，文件不变。</summary>
    public ErrorOr<Success> Apply(Action<JsonObject> mutate)
    {
        JsonObject candidate = _store.Snapshot();
        try
        {
            mutate(candidate);
        }
        catch (InvalidOperationException ex)
        {
            return [Error.Validation("Settings.Path", ex.Message)];
        }

        ErrorOr<KuroeSettings> next = KuroeSettings.From(candidate);
        if (next.IsError)
        {
            List<Error> errors = [Error.Validation("Settings.Invalid", "改动后的配置无效")];
            errors.AddRange(next.ErrorsOrEmptyList);

            return errors;
        }

        try
        {
            _store.Commit(candidate);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [Error.Failure("Settings.Write", $"写入 {_paths.UserSettingsFile} 失败：{ex.Message}")];
        }

        Current = next.Value;

        return Result.Success;
    }

    /// <summary>读取用户层中该路径的设置，未设置时返回 null。</summary>
    public string? TryGetUserValue(string path) => _store.TryGetValue(path);

    /// <summary>路径是否对应已定义的设置项，未定义的路径不会生效。</summary>
    public bool IsConfigured(string path)
    {
        object? value = Current;
        foreach (string segment in path.Split(':'))
        {
            PropertyInfo? property = value?.GetType().GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property is null)
            {
                return false;
            }

            value = property.GetValue(value);
        }

        return true;
    }
}
