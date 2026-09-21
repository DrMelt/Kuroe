using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ApiHub.Models;
using ErrorOr;
using Kuroe.Agent;
using Kuroe.Catalogs;
using Kuroe.Configuration;

namespace Kuroe.Cli;

/// <summary>斜杠命令的解析与执行。</summary>
internal static class ReplCommands
{
    private const string ModelPath = $"{AgentSettings.SectionName}:{nameof(AgentSettings.Model)}";

    private static readonly JsonSerializerOptions ViewOptions = new()
    {
        Converters = { new JsonStringEnumConverter(), new ModelNameJsonConverter() },
    };

    public static void Execute(string input, AgentSession session, SettingsProvider settings, CatalogService catalog)
    {
        string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        switch (parts[0].ToLowerInvariant())
        {
            case "/help":
                PrintHelp();
                break;

            case "/reset":
                session.Reset();
                Console.WriteLine("上下文已清空。");
                break;

            case "/config":
                PrintConfig(settings);
                break;

            case "/provider":
                RunProvider(parts, catalog);
                break;

            case "/model":
                RunModel(parts, settings, catalog);
                break;

            case "/catalog":
                RunCatalog(parts, catalog);
                break;

            case "/set":
                Set(input, settings, catalog);
                break;

            case "/unset":
                if (parts.Length != 2)
                {
                    Console.WriteLine("用法：/unset <路径>，例如 /unset Agent:Temperature");
                    break;
                }

                Unset(parts[1], settings);
                break;

            default:
                Console.WriteLine($"未知命令 {parts[0]}，输入 /help 查看命令。");
                break;
        }
    }

    private static void PrintHelp() => Console.WriteLine("""
        /config                            打印生效配置，标记各项是否已写入用户层
        /set <路径> <值>                   写入用户层并立即生效
        /unset <路径>                      删除用户层中的该项
        /provider list                     列出提供商与模型
        /provider add <名> <端点> <凭据>   新增提供商
        /provider key <名> <凭据>          更换提供商凭据
        /provider rm <名>                  删除提供商，仍被模型引用时拒绝
        /model <模型>                      切换模型，要求已注册
        /model add <模型> <提供商>         把模型注册到提供商
        /model rm <模型>                   注销模型
        /catalog export <文件>             导出目录，凭据替换为占位符
        /catalog import <文件>             合并导入目录
        /reset                             清空上下文
        exit                               退出
        """);

    private static void RunProvider(string[] parts, CatalogService catalog)
    {
        const string usage = "用法：/provider list | add <名> <端点> <凭据> | key <名> <凭据> | rm <名>";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch (parts.Length)
        {
            case 2 when subcommand == "list":
                PrintCatalog(catalog.Snapshot());
                break;

            case 3 when subcommand == "rm":
                RemoveProvider(parts[2], catalog);
                break;

            case 4 when subcommand == "key":
                SetProviderKey(parts[2], parts[3], catalog);
                break;

            case 5 when subcommand == "add":
                AddProvider(parts[2], parts[3], parts[4], catalog);
                break;

            default:
                Console.WriteLine(usage);
                break;
        }
    }

    private static void AddProvider(string name, string baseAddress, string apiKey, CatalogService catalog)
    {
        ErrorOr<ProviderName> providerName = ProviderName.Create(name);
        ErrorOr<ProviderEndpoint> endpoint = ProviderEndpoint.Create(baseAddress);
        ErrorOr<ApiKey> key = ApiKey.Create(apiKey);

        List<Error> errors = [];
        Collect(providerName, errors);
        Collect(endpoint, errors);
        Collect(key, errors);
        if (errors.Count > 0)
        {
            Reject(errors);
            return;
        }

        Report(catalog.AddProvider(providerName.Value, endpoint.Value, key.Value), "已保存。");
    }

    private static void SetProviderKey(string name, string apiKey, CatalogService catalog)
    {
        ErrorOr<ProviderName> providerName = ProviderName.Create(name);
        ErrorOr<ApiKey> key = ApiKey.Create(apiKey);

        List<Error> errors = [];
        Collect(providerName, errors);
        Collect(key, errors);
        if (errors.Count > 0)
        {
            Reject(errors);
            return;
        }

        Report(catalog.SetProviderKey(providerName.Value, key.Value), "已保存。");
    }

    private static void RemoveProvider(string name, CatalogService catalog)
    {
        ErrorOr<ProviderName> providerName = ProviderName.Create(name);
        if (providerName.IsError)
        {
            Reject(providerName.ErrorsOrEmptyList);
            return;
        }

        Report(catalog.RemoveProvider(providerName.Value), "已删除。");
    }

    private static void PrintCatalog(CatalogContents contents)
    {
        if (contents.Providers.Length == 0)
        {
            Console.WriteLine("目录为空，用 /provider add <名> <端点> <凭据> 添加提供商。");
            return;
        }

        Console.WriteLine("提供商：");
        foreach (ProviderDefinition provider in contents.Providers)
        {
            string key = provider.ApiKey.Value == CatalogService.PlaceholderApiKey ? "凭据是占位符" : "凭据已设置";

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

    private static void RunModel(string[] parts, SettingsProvider settings, CatalogService catalog)
    {
        const string usage = "用法：/model <模型> 切换，/model add <模型> <提供商> 注册，/model rm <模型> 注销";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch (parts.Length)
        {
            case 1:
                Console.WriteLine($"当前模型 {settings.Current.Agent.Model.Value}。{usage}");
                break;

            case 2 when subcommand is not ("add" or "rm"):
                SelectModel(parts[1], settings, catalog);
                break;

            case 3 when subcommand == "rm":
                RemoveModel(parts[2], catalog);
                break;

            case 4 when subcommand == "add":
                AddModel(parts[2], parts[3], catalog);
                break;

            default:
                Console.WriteLine(usage);
                break;
        }
    }

    private static void AddModel(string modelName, string providerName, CatalogService catalog)
    {
        ErrorOr<ModelName> model = ModelName.Create(modelName);
        ErrorOr<ProviderName> provider = ProviderName.Create(providerName);

        List<Error> errors = [];
        Collect(model, errors);
        Collect(provider, errors);
        if (errors.Count > 0)
        {
            Reject(errors);
            return;
        }

        Report(catalog.AddModel(model.Value, provider.Value), "已注册。");
    }

    private static void RemoveModel(string modelName, CatalogService catalog)
    {
        ErrorOr<ModelName> model = ModelName.Create(modelName);
        if (model.IsError)
        {
            Reject(model.ErrorsOrEmptyList);
            return;
        }

        Report(catalog.RemoveModel(model.Value), "已注销。");
    }

    private static void SelectModel(string modelName, SettingsProvider settings, CatalogService catalog)
    {
        if (!IsRegistered(modelName, catalog))
        {
            return;
        }

        Apply(ModelPath, JsonValue.Create(modelName)!, settings);
    }

    /// <summary>模型必须已在目录中注册，否则写进偏好只让下一轮对话失败。</summary>
    private static bool IsRegistered(string modelName, CatalogService catalog)
    {
        ErrorOr<ModelName> model = ModelName.Create(modelName);
        if (model.IsError)
        {
            Reject(model.ErrorsOrEmptyList);
            return false;
        }

        ErrorOr<ModelConnection> connection = catalog.Connect(model.Value);
        if (connection.IsError)
        {
            Reject(connection.ErrorsOrEmptyList);
            Errors.GuideModelRegistration(catalog, model.Value);
            return false;
        }

        return true;
    }

    private static void RunCatalog(string[] parts, CatalogService catalog)
    {
        const string usage = "用法：/catalog export <文件> | import <文件>";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch (parts.Length)
        {
            case 3 when subcommand == "export":
                Report(catalog.Export(parts[2]), $"已导出，凭据以 {CatalogService.PlaceholderApiKey} 占位。");
                break;

            case 3 when subcommand == "import":
                Import(parts[2], catalog);
                break;

            default:
                Console.WriteLine(usage);
                break;
        }
    }

    private static void Import(string file, CatalogService catalog)
    {
        ErrorOr<IReadOnlyList<string>> imported = catalog.Import(file);
        if (imported.IsError)
        {
            Reject(imported.ErrorsOrEmptyList);
            return;
        }

        Console.WriteLine("已合并导入。");
        foreach (string note in imported.Value)
        {
            Console.WriteLine($"  {note}");
        }

        string[] masked = [.. catalog.Snapshot().Providers
            .Where(provider => provider.ApiKey.Value == CatalogService.PlaceholderApiKey)
            .Select(provider => provider.ProviderName.Value)];
        if (masked.Length > 0)
        {
            Console.WriteLine($"用 /provider key <提供商> <凭据> 替换占位符凭据：{string.Join('、', masked)}");
        }
    }

    private static void Set(string input, SettingsProvider settings, CatalogService catalog)
    {
        string[] parts = input.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            Console.WriteLine("用法：/set <路径> <值>，例如 /set Agent:Temperature 0.7");
            return;
        }

        JsonNode? value = ParseValue(parts[2]);
        if (value is null)
        {
            Console.WriteLine("值不能为 null，清除设置请用 /unset。");
            return;
        }

        if (parts[1].Equals(ModelPath, StringComparison.OrdinalIgnoreCase) && !IsRegistered(value.ToString(), catalog))
        {
            return;
        }

        Apply(parts[1], value, settings);
    }

    private static void Unset(string path, SettingsProvider settings)
    {
        if (settings.TryGetUserValue(path) is null)
        {
            Console.WriteLine($"{path} 未在用户层设置。");
            return;
        }

        AgentSettings before = settings.Current.Agent;
        ErrorOr<Success> removed = settings.Apply(root => UserSettingsStore.RemoveValue(root, path));
        if (removed.IsError)
        {
            Reject(removed.ErrorsOrEmptyList);
            return;
        }

        ReportApplied(before, settings);
    }

    /// <summary>值先按 JSON 字面量解析，解析失败按字符串写入。</summary>
    private static JsonNode? ParseValue(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text);
        }
    }

    private static void Apply(string path, JsonNode value, SettingsProvider settings)
    {
        AgentSettings before = settings.Current.Agent;
        ErrorOr<Success> saved = settings.Apply(root => UserSettingsStore.SetValue(root, path, value));
        if (saved.IsError)
        {
            Reject(saved.ErrorsOrEmptyList);
            return;
        }

        if (!settings.IsConfigured(path))
        {
            Console.WriteLine($"已写入用户层，但生效配置中没有 {path}，不会起作用。");
            return;
        }

        ReportApplied(before, settings);
    }

    /// <summary>设置写入落盘后的提示。</summary>
    private static void ReportApplied(AgentSettings before, SettingsProvider settings)
    {
        AgentSettings current = settings.Current.Agent;

        Console.WriteLine(current.RequiresRestart(before) ? "已保存，重启后生效。" : "已保存并生效。");
        if (current.InvalidatesSession(before))
        {
            Console.WriteLine("下一轮对话将清空上下文。");
        }
    }

    private static void Report(ErrorOr<Success> result, string done)
    {
        if (result.IsError)
        {
            Reject(result.ErrorsOrEmptyList);
            return;
        }

        Console.WriteLine(done);
    }

    private static void Reject(IEnumerable<Error> errors)
    {
        Errors.Report(errors);
        Console.WriteLine("未生效。");
    }

    private static void Collect<T>(ErrorOr<T> parsed, List<Error> errors)
    {
        if (parsed.IsError)
        {
            errors.AddRange(parsed.ErrorsOrEmptyList);
        }
    }

    private static void PrintConfig(SettingsProvider settings)
    {
        Console.WriteLine($"用户层文件：{settings.UserSettingsFile}");

        JsonObject view = JsonSerializer.SerializeToNode(settings.Current, ViewOptions)!.AsObject();
        foreach ((string section, JsonNode? body) in view)
        {
            Console.WriteLine($"{section}:");
            foreach ((string key, JsonNode? value) in body!.AsObject())
            {
                string text = value is JsonValue scalar ? scalar.ToString() : "未设置";
                string path = $"{section}:{key}";
                bool written = settings.TryGetUserValue(path) is not null;

                Console.WriteLine($"  {key} = {text}（{(written ? "用户层已设置" : "用户层未设置")}）");
            }
        }
    }

    /// <summary>让模型名在 /config 中显示为原文。</summary>
    private sealed class ModelNameJsonConverter : JsonConverter<ModelName>
    {
        public override ModelName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, ModelName value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
