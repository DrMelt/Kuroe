using ApiHub.Catalogs;
using ApiHub.Models;
using ErrorOr;

namespace Kuroe.Catalogs;

/// <summary>运行期的提供商与模型目录。改动在锁内基于副本进行，落盘成功后才换快照。</summary>
public sealed class CatalogService
{
    /// <summary>导出时替代凭据原文的占位文本，导入后需要替换为真实凭据。</summary>
    public const string PlaceholderApiKey = "***";

    private static readonly ApiKey MaskedApiKey = ApiKey.Create(PlaceholderApiKey).Value;

    private readonly CatalogStore _store;
    private readonly Lock _gate = new();
    private Catalog _catalog;

    private CatalogService(CatalogStore store, CatalogContents contents)
    {
        _store = store;
        _catalog = new Catalog(contents);
    }

    /// <summary>从目录文件装配目录。</summary>
    public static ErrorOr<CatalogService> Create(CatalogStore store)
    {
        ErrorOr<CatalogContents> loaded = store.Load();

        return loaded.IsError ? loaded.ErrorsOrEmptyList : new CatalogService(store, loaded.Value);
    }

    /// <summary>目录当前快照，供列出与展示。</summary>
    public CatalogContents Snapshot()
    {
        lock (_gate)
        {
            return _catalog.Contents;
        }
    }

    /// <summary>按模型名解析接入信息，模型未注册时返回错误。</summary>
    public ErrorOr<ModelConnection> Connect(ModelName modelName)
    {
        lock (_gate)
        {
            ModelConnection? connection = _catalog.FindModelConnection(modelName);
            if (connection is not null)
            {
                return connection;
            }

            return [Error.NotFound(
                "Catalog.ModelNotRegistered",
                $"目录中没有模型 {modelName.Value}。")];
        }
    }

    public ErrorOr<Success> AddProvider(ProviderName providerName, ProviderEndpoint baseAddress, ApiKey apiKey) =>
        Mutate(catalog => catalog.AddProvider(ProviderDefinition.Create(providerName, baseAddress, apiKey)));

    /// <summary>更换提供商凭据，提供商不存在时返回错误。</summary>
    public ErrorOr<Success> SetProviderKey(ProviderName providerName, ApiKey apiKey) =>
        Mutate(catalog =>
        {
            ProviderDefinition? provider = catalog.FindProvider(providerName);
            if (provider is null)
            {
                return [Error.NotFound("Catalog.ProviderNotFound", $"提供商 {providerName.Value} 不存在。")];
            }

            return catalog.ReplaceProvider(provider with { ApiKey = apiKey });
        });

    /// <summary>删除提供商，仍被模型引用时由目录拒绝。</summary>
    public ErrorOr<Success> RemoveProvider(ProviderName providerName) =>
        Mutate(catalog => catalog.RemoveProvider(providerName));

    public ErrorOr<Success> AddModel(ModelName modelName, ProviderName providerName) =>
        Mutate(catalog => catalog.AddModel(ModelDefinition.Create(modelName, providerName)));

    public ErrorOr<Success> RemoveModel(ModelName modelName) =>
        Mutate(catalog => catalog.RemoveModel(modelName));

    /// <summary>导出脱敏目录，凭据替换为占位文本。</summary>
    public ErrorOr<Success> Export(string target)
    {
        lock (_gate)
        {
            ErrorOr<CatalogContents> masked = CatalogContents.Create(
                [.. _catalog.Providers.Select(provider => provider with { ApiKey = MaskedApiKey })],
                [.. _catalog.Models]);

            return masked.IsError ? masked.ErrorsOrEmptyList : CatalogStore.Write(target, masked.Value);
        }
    }

    /// <summary>合并导入：逐条添加，冲突跳过并汇总为说明，已有条目不受影响。</summary>
    public ErrorOr<IReadOnlyList<string>> Import(string source)
    {
        ErrorOr<CatalogContents> parsed = CatalogStore.Read(source);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        List<string> notes = [];
        ErrorOr<Success> merged = Mutate(catalog =>
        {
            foreach (ProviderDefinition provider in parsed.Value.Providers)
            {
                string name = provider.ProviderName.Value;
                ErrorOr<Success> added = catalog.AddProvider(provider);
                if (added.IsError)
                {
                    notes.Add($"提供商 {name} 未导入：{added.FirstError.Description}");
                }
                else if (provider.ApiKey.Value == PlaceholderApiKey)
                {
                    notes.Add($"提供商 {name} 的凭据是占位符，使用前需要替换。");
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

            return Result.Success;
        });

        return merged.IsError ? merged.ErrorsOrEmptyList : notes;
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
