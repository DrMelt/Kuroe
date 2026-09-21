using ApiHub.Models;
using ErrorOr;
using Kuroe.Catalogs;

namespace Kuroe.Cli.Commands;

/// <summary>/provider 子命令的解析与执行。</summary>
internal sealed class ProviderCommands(CatalogService catalog)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/provider list", "列出提供商与模型"),
        ("/provider add <名> <端点> <凭据>", "新增提供商"),
        ("/provider key <名> <凭据>", "更换提供商凭据"),
        ("/provider rm <名>", "删除提供商，仍被模型引用时拒绝"),
    ];

    private readonly CatalogService _catalog = catalog;

    public void Run(string[] parts)
    {
        const string usage = "用法：/provider list | add <名> <端点> <凭据> | key <名> <凭据> | rm <名>";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch ((subcommand, parts.Length))
        {
            case ("list", 2):
                Print(_catalog.Snapshot());
                break;

            case ("rm", 3):
                Remove(parts[2]);
                break;

            case ("key", 4):
                SetKey(parts[2], parts[3]);
                break;

            case ("add", 5):
                Add(parts[2], parts[3], parts[4]);
                break;

            default:
                Console.WriteLine(usage);
                break;
        }
    }

    private void Add(string name, string baseAddress, string apiKey)
    {
        ErrorOr<ProviderName> providerName = ProviderName.Create(name);
        ErrorOr<ProviderEndpoint> endpoint = ProviderEndpoint.Create(baseAddress);
        ErrorOr<ApiKey> key = ApiKey.Create(apiKey);

        List<Error> errors = [];
        ConsoleResults.Collect(providerName, errors);
        ConsoleResults.Collect(endpoint, errors);
        ConsoleResults.Collect(key, errors);
        if (errors.Count > 0)
        {
            ConsoleResults.Reject(errors);
            return;
        }

        ConsoleResults.Report(_catalog.AddProvider(providerName.Value, endpoint.Value, key.Value), "已保存。");
    }

    private void SetKey(string name, string apiKey)
    {
        ErrorOr<ProviderName> providerName = ProviderName.Create(name);
        ErrorOr<ApiKey> key = ApiKey.Create(apiKey);

        List<Error> errors = [];
        ConsoleResults.Collect(providerName, errors);
        ConsoleResults.Collect(key, errors);
        if (errors.Count > 0)
        {
            ConsoleResults.Reject(errors);
            return;
        }

        ConsoleResults.Report(_catalog.SetProviderKey(providerName.Value, key.Value), "已保存。");
    }

    private void Remove(string name)
    {
        ErrorOr<ProviderName> providerName = ProviderName.Create(name);
        if (providerName.IsError)
        {
            ConsoleResults.Reject(providerName.ErrorsOrEmptyList);
            return;
        }

        ConsoleResults.Report(_catalog.RemoveProvider(providerName.Value), "已删除。");
    }

    /// <summary>列出提供商与模型，凭据只显示是否已设置。</summary>
    private static void Print(CatalogContents contents)
    {
        if (contents.Providers.Length == 0)
        {
            Console.WriteLine("目录为空，用 /provider add <名> <端点> <凭据> 添加提供商。");
            return;
        }

        Console.WriteLine("提供商：");
        foreach (ProviderDefinition provider in contents.Providers)
        {
            string key = CatalogService.IsPlaceholder(provider.ApiKey) ? "凭据是占位符" : "凭据已设置";

            Console.WriteLine($"  {provider.ProviderName.Value}  {provider.BaseAddress.Address}  {key}");
        }

        if (contents.Models.Length == 0)
        {
            Console.WriteLine("模型：无，用 /model add <模型> <提供商> 注册。");
            return;
        }

        Console.WriteLine("模型：");
        foreach (ModelDefinition model in contents.Models)
        {
            Console.WriteLine($"  {model.ModelName.Value} → {model.ProviderName.Value}");
        }
    }
}