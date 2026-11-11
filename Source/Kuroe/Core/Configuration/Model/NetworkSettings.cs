using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ErrorOr;
using Kuroe.Configuration.Files;

namespace Kuroe.Configuration.Model;

/// <summary>网络代理偏好，绑定自用户层的 Network 节。只作用于本进程内的默认 HttpClient。</summary>
internal sealed record NetworkSettings
{
    public const string SectionName = "Network";

    private const string SystemMode = "system";

    private const string DirectMode = "direct";

    private static readonly string[] SupportedSchemes = ["http", "https", "socks4", "socks4a", "socks5"];

    /// <summary>代理模式或代理地址。未设置、system 与 direct 之外的值必须是绝对地址。</summary>
    public string? Proxy { get; init; }

    /// <summary>代理在进程启动时写入默认 HttpClient，改动重启后生效。</summary>
    public bool RequiresRestart(NetworkSettings other) => Proxy != other.Proxy;

    /// <summary>绑定并校验该节，未写的设置项保持未设置。值的合法性在这里判定。</summary>
    public static ErrorOr<NetworkSettings> From(JsonObject? section)
    {
        NetworkSectionDto? file;
        try
        {
            file = section?.Deserialize(SettingsJson.Default.NetworkSectionDto);
        }
        catch (JsonException ex)
        {
            return [SettingsErrors.Bind(ex.Message)];
        }

        if (file is null)
        {
            return new NetworkSettings();
        }

        string? proxy = Normalize(file.Proxy);
        if (proxy is not null)
        {
            if (IsSystem(proxy))
            {
                proxy = SystemMode;
            }
            else if (IsDirect(proxy))
            {
                proxy = DirectMode;
            }
            else if (!IsProxyAddress(proxy))
            {
                return [SettingsErrors.Proxy(proxy)];
            }
        }

        return new NetworkSettings { Proxy = proxy };
    }

    /// <summary>按模式构造默认 HttpClient 使用的代理。系统代理返回 null，表示不修改进程默认值。</summary>
    internal IWebProxy? CreateProxy() => Proxy switch
    {
        null => null,
        { } proxy when IsSystem(proxy) => null,
        { } proxy when IsDirect(proxy) => new WebProxy(),
        { } proxy => CustomProxy(proxy),
    };

    /// <summary>把代理设置写入进程默认代理。系统代理不写入，保留运行时已解析的系统设置。</summary>
    internal void Apply()
    {
        if (CreateProxy() is { } proxy)
        {
            HttpClient.DefaultProxy = proxy;
        }
    }

    private static WebProxy CustomProxy(string address) => new WebProxy(new Uri(address));

    private static bool IsProxyAddress(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? address)
        && address is not null
        && SupportedSchemes.Contains(address.Scheme, StringComparer.Ordinal);

    private static bool IsSystem(string value) => value.Equals(SystemMode, StringComparison.OrdinalIgnoreCase);

    private static bool IsDirect(string value) => value.Equals(DirectMode, StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>用户层里 Network 节的形状。未写的项为 null。</summary>
internal sealed class NetworkSectionDto
{
    public string? Proxy { get; set; }
}