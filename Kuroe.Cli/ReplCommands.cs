using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ApiHub.Models;
using ErrorOr;
using Kuroe.Agent;
using Kuroe.Catalogs;
using Kuroe.Configuration;

namespace Kuroe.Cli;

/// <summary>启动提示与斜杠命令的解析与执行。</summary>
internal sealed class ReplCommands(
    AgentSession session,
    SettingsProvider settings,
    CatalogService catalog,
    ToolCollection tools)
{
    private const string ModelPath = $"{AgentSettings.SectionName}:{nameof(AgentSettings.Model)}";

    private static readonly JsonSerializerOptions ViewOptions = new()
    {
        Converters = { new JsonStringEnumConverter(), new ModelNameJsonConverter() },
    };

    private readonly AgentSession _session = session;
    private readonly SettingsProvider _settings = settings;
    private readonly CatalogService _catalog = catalog;
    private readonly ToolCollection _tools = tools;

    public void Execute(string input)
    {
        string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        switch (parts[0].ToLowerInvariant())
        {
            case "/help":
                PrintHelp();
                break;

            case "/reset":
                _session.Reset();
                Console.WriteLine("上下文已清空。");
                break;

            case "/config":
                PrintConfig();
                break;

            case "/provider":
                RunProvider(parts);
                break;

            case "/model":
                RunModel(parts);
                break;

            case "/catalog":
                RunCatalog(parts);
                break;

            case "/set":
                Set(input);
                break;

            case "/unset":
                if (parts.Length != 2)
                {
                    Console.WriteLine("用法：/unset <路径>，例如 /unset Agent:Temperature");
                    break;
                }

                Unset(parts[1]);
                break;

            default:
                Console.WriteLine($"未知命令 {parts[0]}，输入 /help 查看命令。");
                break;
        }
    }

    /// <summary>启动横幅：当前模型、目录规模，以及缺少提供商或工具时的处理指引。</summary>
    public void PrintStartup()
    {
        CatalogContents contents = _catalog.Snapshot();
        string model = _settings.Current.Agent.Model?.Value ?? "未选择";
        Console.WriteLine($"Kuroe 已启动，当前模型 {model}，" +
            $"目录中有 {contents.Providers.Length} 个提供商、{contents.Models.Length} 个模型。");
        if (contents.Providers.Length == 0)
        {
            Console.WriteLine("先 /provider add <提供商> <端点> <凭据> 添加提供商，再 /model add <模型> <提供商> 注册模型。");
        }

        if (_tools.Tools.Count == 0)
        {
            Console.WriteLine("当前没有可用工具，检查是否注册了工具载体、公开方法是否标注 DescriptionAttribute。");
        }

        Console.WriteLine("输入 exit 退出，/help 查看命令，Ctrl+C 中断当前回复。");
    }

    /// <summary>当前模型无法连接时的注册指引，模型可用时没有输出。</summary>
    public void GuideCurrentModel() => Errors.GuideModelRegistration(_catalog, _settings.Current.Agent.Model);

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

    private void RunProvider(string[] parts)
    {
        const string usage = "用法：/provider list | add <名> <端点> <凭据> | key <名> <凭据> | rm <名>";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch (parts.Length)
        {
            case 2 when subcommand == "list":
                PrintCatalog(_catalog.Snapshot());
                break;

            case 3 when subcommand == "rm":
                RemoveProvider(parts[2]);
                break;

            case 4 when subcommand == "key":
                SetProviderKey(parts[2], parts[3]);
                break;

            case 5 when subcommand == "add":
                AddProvider(parts[2], parts[3], parts[4]);
                break;

            default:
                Console.WriteLine(usage);
                break;
        }
    }

    private void AddProvider(string name, string baseAddress, string apiKey)
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

        Report(_catalog.AddProvider(providerName.Value, endpoint.Value, key.Value), "已保存。");
    }

    private void SetProviderKey(string name, string apiKey)
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

        Report(_catalog.SetProviderKey(providerName.Value, key.Value), "已保存。");
    }

    private void RemoveProvider(string name)
    {
        ErrorOr<ProviderName> providerName = ProviderName.Create(name);
        if (providerName.IsError)
        {
            Reject(providerName.ErrorsOrEmptyList);
            return;
        }

        Report(_catalog.RemoveProvider(providerName.Value), "已删除。");
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

    private void RunModel(string[] parts)
    {
        const string usage = "用法：/model <模型> 切换，/model add <模型> <提供商> 注册，/model rm <模型> 注销";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch (parts.Length)
        {
            case 1:
                Console.WriteLine($"当前模型 {_settings.Current.Agent.Model?.Value ?? "未选择"}。{usage}");
                break;

            case 2 when subcommand is not ("add" or "rm"):
                SelectModel(parts[1]);
                break;

            case 3 when subcommand == "rm":
                RemoveModel(parts[2]);
                break;

            case 4 when subcommand == "add":
                AddModel(parts[2], parts[3]);
                break;

            default:
                Console.WriteLine(usage);
                break;
        }
    }

    private void AddModel(string modelName, string providerName)
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

        Report(_catalog.AddModel(model.Value, provider.Value), "已注册。");
    }

    private void RemoveModel(string modelName)
    {
        ErrorOr<ModelName> model = ModelName.Create(modelName);
        if (model.IsError)
        {
            Reject(model.ErrorsOrEmptyList);
            return;
        }

        Report(_catalog.RemoveModel(model.Value), "已注销。");
    }

    private void SelectModel(string modelName)
    {
        if (!IsRegistered(modelName))
        {
            return;
        }

        Apply(ModelPath, JsonValue.Create(modelName)!);
    }

    /// <summary>模型必须已在目录中注册，否则写进偏好只让下一轮对话失败。</summary>
    private bool IsRegistered(string modelName)
    {
        ErrorOr<ModelName> model = ModelName.Create(modelName);
        if (model.IsError)
        {
            Reject(model.ErrorsOrEmptyList);
            return false;
        }

        ErrorOr<ModelConnection> connection = _catalog.Connect(model.Value);
        if (connection.IsError)
        {
            Reject(connection.ErrorsOrEmptyList);
            Errors.GuideModelRegistration(_catalog, model.Value);
            return false;
        }

        return true;
    }

    private void RunCatalog(string[] parts)
    {
        const string usage = "用法：/catalog export <文件> | import <文件>";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch (parts.Length)
        {
            case 3 when subcommand == "export":
                Report(_catalog.Export(parts[2]), $"已导出，凭据以 {CatalogService.PlaceholderApiKey} 占位。");
                break;

            case 3 when subcommand == "import":
                Import(parts[2]);
                break;

            default:
                Console.WriteLine(usage);
                break;
        }
    }

    private void Import(string file)
    {
        ErrorOr<IReadOnlyList<string>> imported = _catalog.Import(file);
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

        string[] masked = [.. _catalog.Snapshot().Providers
            .Where(provider => provider.ApiKey.Value == CatalogService.PlaceholderApiKey)
            .Select(provider => provider.ProviderName.Value)];
        if (masked.Length > 0)
        {
            Console.WriteLine($"用 /provider key <提供商> <凭据> 替换占位符凭据：{string.Join('、', masked)}");
        }
    }

    private void Set(string input)
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

        if (parts[1].Equals(ModelPath, StringComparison.OrdinalIgnoreCase) && !IsRegistered(value.ToString()))
        {
            return;
        }

        Apply(parts[1], value);
    }

    private void Unset(string path)
    {
        if (_settings.TryGetUserValue(path) is null)
        {
            Console.WriteLine($"{path} 未在用户层设置。");
            return;
        }

        AgentSettings before = _settings.Current.Agent;
        ErrorOr<Success> removed = _settings.Apply(root => UserSettingsStore.RemoveValue(root, path));
        if (removed.IsError)
        {
            Reject(removed.ErrorsOrEmptyList);
            return;
        }

        ReportApplied(before);
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

    private void Apply(string path, JsonNode value)
    {
        AgentSettings before = _settings.Current.Agent;
        ErrorOr<Success> saved = _settings.Apply(root => UserSettingsStore.SetValue(root, path, value));
        if (saved.IsError)
        {
            Reject(saved.ErrorsOrEmptyList);
            return;
        }

        if (!_settings.IsConfigured(path))
        {
            Console.WriteLine($"已写入用户层，但生效配置中没有 {path}，不会起作用。");
            return;
        }

        ReportApplied(before);
    }

    /// <summary>设置写入落盘后的提示。</summary>
    private void ReportApplied(AgentSettings before)
    {
        AgentSettings current = _settings.Current.Agent;

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

    private void PrintConfig()
    {
        Console.WriteLine($"用户层文件：{_settings.UserSettingsFile}");

        JsonObject view = JsonSerializer.SerializeToNode(_settings.Current, ViewOptions)!.AsObject();
        foreach ((string section, JsonNode? body) in view)
        {
            Console.WriteLine($"{section}:");
            foreach ((string key, JsonNode? value) in body!.AsObject())
            {
                string text = value is JsonValue scalar ? scalar.ToString() : "未设置";
                string path = $"{section}:{key}";
                bool written = _settings.TryGetUserValue(path) is not null;

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
