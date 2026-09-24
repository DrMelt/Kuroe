using System.Text.Json;
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

    /// <summary>当前生效的配置。呈现由 <see cref="Sections"/> 给出，改动只经 Set 与 Clear。</summary>
    internal KuroeSettings Current { get; private set; }

    /// <summary>用户层偏好文件的绝对路径。</summary>
    public string UserSettingsFile => _paths.UserSettingsFile;

    /// <summary>规范化用户层并绑定校验，失败时一次给出全部错误。装配统一由 AddKuroe 完成。</summary>
    internal static ErrorOr<SettingsProvider> Create(KuroePaths paths)
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

    /// <summary>写入该设置并立即生效。值先按 JSON 字面量解析，不是合法 JSON 时按字符串写入，写成 JSON null 时拒绝。
    /// 路径无效或校验失败时返回错误，文件不变。</summary>
    public ErrorOr<SettingsEffect> Set(string path, string value)
    {
        JsonNode? parsed = ParseValue(value);

        return parsed is null
            ? [SettingsErrors.NullValue()]
            : WithResolved(path, (root, resolved) => UserSettingsStore.SetValue(root, resolved, parsed));
    }

    /// <summary>按文本写入该设置，不按 JSON 字面量解析。写入由命令认定的名称一类文本，值的类型由设置项自身决定。</summary>
    public ErrorOr<SettingsEffect> SetText(string path, string value) =>
        WithResolved(path, (root, resolved) => UserSettingsStore.SetValue(root, resolved, JsonValue.Create(value)!));

    /// <summary>删除用户层中该路径的项。路径无效或校验失败时返回错误，文件不变。</summary>
    public ErrorOr<SettingsEffect> Clear(string path) =>
        WithResolved(path, (root, resolved) => UserSettingsStore.RemoveValue(root, resolved));

    /// <summary>各配置节各设置项的生效值与来源，供宿主直接列出。</summary>
    public IReadOnlyList<SettingSection> Sections() => Current.Sections(_store);

    /// <summary>读取用户层中该路径的设置，未设置时返回 null。路径须是 <see cref="ResolvePath"/> 给出的规范路径。</summary>
    public string? TryGetUserValue(string path) => _store.TryGetValue(path);

    /// <summary>把用户输入的路径解析为规范路径，读写与查询共用同一套路径规则。未定义的路径返回错误；
    /// 模型选择归 <see cref="Catalogs.ModelService"/>，按路径写入一律拒绝。</summary>
    public static ErrorOr<string> ResolvePath(string path)
    {
        ErrorOr<string> resolved = KuroeSettings.ResolvePath(path);
        if (resolved.IsError)
        {
            return resolved;
        }

        return resolved.Value == AgentSettings.ModelPath
            ? [SettingsErrors.ModelPath()]
            : resolved.Value;
    }

    /// <summary>写入或清除模型选择，注册状态的校验由 <see cref="Catalogs.ModelService"/> 负责，因此不经 <see cref="ResolvePath"/>。</summary>
    internal ErrorOr<SettingsEffect> SetModel(string? model) => model is null
        ? Apply(root => UserSettingsStore.RemoveValue(root, AgentSettings.ModelPath))
        : Apply(root => UserSettingsStore.SetValue(root, AgentSettings.ModelPath, JsonValue.Create(model)!));

    private ErrorOr<SettingsEffect> WithResolved(string path, Action<JsonObject, string> mutate)
    {
        ErrorOr<string> resolved = ResolvePath(path);

        return resolved.IsError
            ? resolved.ErrorsOrEmptyList
            : Apply(root => mutate(root, resolved.Value));
    }

    /// <summary>值文本转节点，不是合法 JSON 时按字符串处理。</summary>
    private static JsonNode? ParseValue(string value)
    {
        try
        {
            return JsonNode.Parse(value);
        }
        catch (JsonException)
        {
            return JsonValue.Create(value);
        }
    }

    /// <summary>改动用户层并立即生效，成功时给出该改动的影响。校验失败时返回错误，文件不变。</summary>
    private ErrorOr<SettingsEffect> Apply(Action<JsonObject> mutate)
    {
        AgentSettings before = Current.Agent;

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
        AgentSettings after = Current.Agent;

        return new SettingsEffect(after.RequiresRestart(before), after.InvalidatesHistory(before));
    }
}
