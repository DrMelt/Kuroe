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

    /// <summary>规范化用户层并绑定校验，失败时一次给出全部错误。</summary>
    public static ErrorOr<SettingsProvider> Create(KuroePaths paths)
    {
        UserSettingsStore store;
        try
        {
            store = new UserSettingsStore(paths.UserSettingsFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return [SettingsErrors.UserFile(paths.UserSettingsFile, ex.Message)];
        }

        ErrorOr<JsonObject> normalized = KuroeSettings.Normalize(store.Snapshot());
        if (normalized.IsError)
        {
            return normalized.ErrorsOrEmptyList;
        }

        ErrorOr<KuroeSettings> bound = KuroeSettings.From(normalized.Value);
        if (bound.IsError)
        {
            return bound.ErrorsOrEmptyList;
        }

        store.Adopt(normalized.Value);

        return new SettingsProvider(paths, store, bound.Value);
    }

    /// <summary>写入该设置并立即生效，路径按规范路径落盘。路径无效或校验失败时返回错误，文件不变。</summary>
    public ErrorOr<Success> Set(string path, JsonNode value) =>
        WithResolved(path, (root, resolved) => UserSettingsStore.SetValue(root, resolved, value));

    /// <summary>删除该设置在用户层中的项。路径无效或校验失败时返回错误，文件不变。</summary>
    public ErrorOr<Success> Clear(string path) =>
        WithResolved(path, (root, resolved) => UserSettingsStore.RemoveValue(root, resolved));

    /// <summary>读取用户层中该路径的设置，未设置时返回 null。</summary>
    public string? TryGetUserValue(string path) => _store.TryGetValue(path);

    private ErrorOr<Success> WithResolved(string path, Action<JsonObject, string> mutate)
    {
        ErrorOr<string> resolved = KuroeSettings.ResolvePath(path);

        return resolved.IsError
            ? resolved.ErrorsOrEmptyList
            : Apply(root => mutate(root, resolved.Value));
    }

    /// <summary>改动用户层并立即生效。校验失败时返回错误，文件不变。</summary>
    private ErrorOr<Success> Apply(Action<JsonObject> mutate)
    {
        JsonObject candidate = _store.Snapshot();
        try
        {
            mutate(candidate);
        }
        catch (InvalidOperationException ex)
        {
            return [SettingsErrors.Path(ex.Message)];
        }

        ErrorOr<KuroeSettings> next = KuroeSettings.From(candidate);
        if (next.IsError)
        {
            List<Error> errors = [SettingsErrors.Invalid()];
            errors.AddRange(next.ErrorsOrEmptyList);

            return errors;
        }

        try
        {
            _store.Commit(candidate);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [SettingsErrors.Write(_paths.UserSettingsFile, ex.Message)];
        }

        Current = next.Value;

        return Result.Success;
    }
}
