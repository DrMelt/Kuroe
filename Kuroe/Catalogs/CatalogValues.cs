using ApiHub.Shared.Models;
using ErrorOr;

namespace Kuroe.Catalogs;

/// <summary>把宿主提交的文本解析为值对象，同一请求内的全部非法项一次给出。</summary>
internal static class CatalogValues
{
    internal static ErrorOr<ModelName> Model(string name) => ModelName.Create(name);

    internal static ErrorOr<ProviderName> Provider(string name) => ProviderName.Create(name);

    internal static ErrorOr<ApiKey> Key(string apiKey) => ApiKey.Create(apiKey);

    internal static ErrorOr<(ProviderName Provider, ApiKey Key)> ProviderKey(string name, string apiKey)
    {
        ErrorOr<ProviderName> provider = ProviderName.Create(name);
        ErrorOr<ApiKey> key = ApiKey.Create(apiKey);

        return AllErrors(provider, key) is { Count: > 0 } errors
            ? errors
            : (provider.Value, key.Value);
    }

    internal static ErrorOr<(ModelName Model, ProviderName Provider)> ModelReference(
        string modelName, string providerName)
    {
        ErrorOr<ModelName> model = ModelName.Create(modelName);
        ErrorOr<ProviderName> provider = ProviderName.Create(providerName);

        return AllErrors(model, provider) is { Count: > 0 } errors
            ? errors
            : (model.Value, provider.Value);
    }

    internal static ErrorOr<(ProviderName Provider, ProviderEndpoint Endpoint, ApiKey Key)> Provider(
        string name, string endpoint, string apiKey)
    {
        ErrorOr<ProviderName> provider = ProviderName.Create(name);
        ErrorOr<ProviderEndpoint> address = ProviderEndpoint.Create(endpoint);
        ErrorOr<ApiKey> key = ApiKey.Create(apiKey);

        return AllErrors(provider, address, key) is { Count: > 0 } errors
            ? errors
            : (provider.Value, address.Value, key.Value);
    }

    /// <summary>全部解析结果的错误并集，无错误时为空列表。</summary>
    private static List<Error> AllErrors<T1, T2>(ErrorOr<T1> first, ErrorOr<T2> second)
    {
        List<Error> errors = [];
        Append(first, errors);
        Append(second, errors);

        return errors;
    }

    private static List<Error> AllErrors<T1, T2, T3>(ErrorOr<T1> first, ErrorOr<T2> second, ErrorOr<T3> third)
    {
        List<Error> errors = [];
        Append(first, errors);
        Append(second, errors);
        Append(third, errors);

        return errors;
    }

    private static void Append<T>(ErrorOr<T> parsed, List<Error> errors)
    {
        if (parsed.IsError)
        {
            errors.AddRange(parsed.ErrorsOrEmptyList);
        }
    }
}
