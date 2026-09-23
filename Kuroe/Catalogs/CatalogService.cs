using ApiHub.Catalogs;
using ApiHub.Shared.Catalogs;
using ApiHub.Shared.Models;
using ErrorOr;

namespace Kuroe.Catalogs;

/// <summary>运行期的提供商与模型目录。改动在锁内基于副本进行，落盘成功后才换快照。</summary>
public sealed class CatalogService
{
    /// <summary>导出时替代凭据原文的占位文本，导入后需要替换为真实凭据。</summary>
    internal const string PlaceholderApiKey = "***";

    private static readonly ApiKey MaskedApiKey = ApiKey.Create(PlaceholderApiKey).Value;

    /// <summary>凭据是否为导出时的占位文本。</summary>
    internal static bool IsPlaceholder(ApiKey apiKey) => apiKey.Value == PlaceholderApiKey;

    private readonly CatalogStore _store;
    private readonly Lock _gate = new();
    private Catalog _catalog;

    private CatalogService(CatalogStore store, CatalogContents contents)
    {
        _store = store;
        _catalog = new Catalog(contents);
    }

    /// <summary>从目录文件装配目录。</summary>
    internal static ErrorOr<CatalogService> Create(CatalogStore store)
    {
        ErrorOr<CatalogContents> loaded = store.Load();

        return loaded.IsError ? loaded.ErrorsOrEmptyList : new CatalogService(store, loaded.Value);
    }

    /// <summary>目录当前快照，供列出与展示。</summary>
    public CatalogSnapshot Snapshot()
    {
        lock (_gate)
        {
            CatalogContents contents = _catalog.Contents;

            return new CatalogSnapshot(
                [.. contents.Providers.Select(provider => new ProviderInfo(
                    provider.ProviderName.Value,
                    provider.BaseAddress.Address.ToString(),
                    IsPlaceholder(provider.ApiKey)))],
                [.. contents.Models.Select(model => new ModelInfo(
                    model.ModelName.Value,
                    model.ProviderName.Value))]);
        }
    }

    /// <summary>模型是否已在目录中注册。</summary>
    internal bool HasModel(string modelName) =>
        CatalogValues.Model(modelName) is { IsError: false } model && HasModel(model.Value);

    private bool HasModel(ModelName modelName)
    {
        lock (_gate)
        {
            return _catalog.FindModel(modelName) is not null;
        }
    }

    /// <summary>按模型名解析接入信息，模型未注册时返回错误。</summary>
    internal ErrorOr<ModelConnection> Connect(string modelName)
    {
        ErrorOr<ModelName> model = CatalogValues.Model(modelName);

        return model.IsError ? model.ErrorsOrEmptyList : Connect(model.Value);
    }

    private ErrorOr<ModelConnection> Connect(ModelName modelName)
    {
        lock (_gate)
        {
            ModelConnection? connection = _catalog.FindModelConnection(modelName);
            if (connection is not null)
            {
                return connection;
            }

            return [CatalogErrors.ModelNotFound(modelName)];
        }
    }

    /// <summary>模型可接入时成功，否则给出接入信息解析的错误，供不持有客户端的调用方判可用性。</summary>
    internal ErrorOr<Success> Check(string modelName)
    {
        ErrorOr<ModelConnection> connection = Connect(modelName);

        return connection.IsError ? connection.ErrorsOrEmptyList : Result.Success;
    }

    public ErrorOr<Success> AddProvider(string name, string baseAddress, string apiKey)
    {
        ErrorOr<(ProviderName Provider, ProviderEndpoint Endpoint, ApiKey Key)> parsed =
            CatalogValues.Provider(name, baseAddress, apiKey);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        (ProviderName provider, ProviderEndpoint endpoint, ApiKey key) = parsed.Value;

        return Mutate(catalog => catalog.AddProvider(ProviderDefinition.Create(provider, endpoint, key)));
    }

    /// <summary>更换提供商凭据，提供商不存在时返回错误。</summary>
    public ErrorOr<Success> SetProviderKey(string name, string apiKey)
    {
        ErrorOr<(ProviderName Provider, ApiKey Key)> parsed = CatalogValues.ProviderKey(name, apiKey);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        (ProviderName provider, ApiKey key) = parsed.Value;

        return Mutate(catalog =>
        {
            ProviderDefinition? existing = catalog.FindProvider(provider);
            if (existing is null)
            {
                return [CatalogErrors.ProviderNotFound(provider)];
            }

            return catalog.ReplaceProvider(existing with { ApiKey = key });
        });
    }

    /// <summary>删除提供商，仍被模型引用时由目录拒绝。</summary>
    public ErrorOr<Success> RemoveProvider(string name)
    {
        ErrorOr<ProviderName> providerName = CatalogValues.Provider(name);

        return providerName.IsError
            ? providerName.ErrorsOrEmptyList
            : Mutate(catalog => catalog.RemoveProvider(providerName.Value));
    }

    public ErrorOr<Success> AddModel(string modelName, string providerName)
    {
        ErrorOr<(ModelName Model, ProviderName Provider)> parsed =
            CatalogValues.ModelReference(modelName, providerName);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        (ModelName model, ProviderName provider) = parsed.Value;

        return Mutate(catalog => catalog.AddModel(ModelDefinition.Create(model, provider)));
    }

    /// <summary>从目录注销模型。注销当前选中模型时的取消选择由 <see cref="Agent.ModelService"/> 负责。</summary>
    internal ErrorOr<Success> RemoveModel(string modelName)
    {
        ErrorOr<ModelName> model = CatalogValues.Model(modelName);

        return model.IsError
            ? model.ErrorsOrEmptyList
            : Mutate(catalog => catalog.RemoveModel(model.Value));
    }

    /// <summary>把文件参数解析为绝对路径，相对参数按目录文件所在目录解析。</summary>
    private ErrorOr<string> ResolveFile(string path)
    {
        try
        {
            return Path.GetFullPath(path, _store.BaseDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return [CatalogFileErrors.InvalidPath(path, ex.Message)];
        }
    }

    /// <summary>导出脱敏目录，凭据替换为占位文本，返回写入的绝对路径。</summary>
    public ErrorOr<string> Export(string target)
    {
        ErrorOr<string> resolved = ResolveFile(target);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        string path = resolved.Value;
        if (_store.IsCatalogFile(path))
        {
            return [CatalogFileErrors.OverwritesCatalog(path)];
        }

        lock (_gate)
        {
            ErrorOr<CatalogContents> masked = CatalogContents.Create(
                [.. _catalog.Providers.Select(provider => provider with { ApiKey = MaskedApiKey })],
                [.. _catalog.Models]);

            if (masked.IsError)
            {
                return masked.ErrorsOrEmptyList;
            }

            ErrorOr<Success> written = CatalogStore.Write(path, masked.Value);

            return written.IsError ? written.ErrorsOrEmptyList : path;
        }
    }

    /// <summary>合并导入：逐条添加，冲突跳过并汇总为说明，已有条目不受影响。</summary>
    public ErrorOr<CatalogMerge> Import(string source)
    {
        ErrorOr<string> resolved = ResolveFile(source);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        string path = resolved.Value;
        ErrorOr<CatalogContents> parsed = CatalogStore.Read(path);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        List<string> notes = [];
        List<string> placeholders = [];
        ErrorOr<Success> merged = Mutate(catalog =>
        {
            foreach (ProviderDefinition provider in parsed.Value.Providers)
            {
                ErrorOr<Success> added = catalog.AddProvider(provider);
                if (added.IsError)
                {
                    notes.Add($"提供商 {provider.ProviderName.Value} 未导入：{added.FirstError.Description}");
                }
            }

            foreach (ModelDefinition model in parsed.Value.Models)
            {
                ErrorOr<Success> added = catalog.AddModel(model);
                if (added.IsError)
                {
                    notes.Add($"模型 {model.ModelName.Value} 未导入：{added.FirstError.Description}");
                }
            }

            placeholders.AddRange(catalog.Providers
                .Where(provider => IsPlaceholder(provider.ApiKey))
                .Select(provider => provider.ProviderName.Value));

            return Result.Success;
        });

        return merged.IsError
            ? merged.ErrorsOrEmptyList
            : new CatalogMerge(path, notes, placeholders);
    }

    private ErrorOr<Success> Mutate(Func<Catalog, ErrorOr<Success>> mutate)
    {
        lock (_gate)
        {
            Catalog candidate = new(_catalog.Contents);

            ErrorOr<Success> changed = mutate(candidate);
            if (changed.IsError)
            {
                return changed.ErrorsOrEmptyList;
            }

            ErrorOr<Success> saved = _store.Save(candidate.Contents);
            if (saved.IsError)
            {
                return saved.ErrorsOrEmptyList;
            }

            _catalog = candidate;

            return Result.Success;
        }
    }
}
