using System.Net;
using System.Text.Json.Nodes;
using Kuroe.Composition.Configuration.Model;
using Kuroe.Composition.Tests.Support;
using Xunit;

namespace Kuroe.Composition.Tests.Configuration;

/// <summary>网络代理模式与地址校验。</summary>
public sealed class NetworkSettingsTests
{
    [Fact(DisplayName = "没有代理时保持进程默认")]
    public void Absent_proxy_keeps_the_process_default()
    {
        NetworkSettings settings = NetworkSettings.From(null).ThrowIfError();

        Assert.Null(settings.Proxy);
        Assert.Null(settings.CreateProxy());
    }

    [Theory]
    [InlineData("system", Label = "系统模式")]
    [InlineData("SYSTEM", Label = "大写系统模式")]
    public void System_mode_keeps_the_process_default(string value)
    {
        NetworkSettings settings = NetworkSettings.From(Section(value)).ThrowIfError();

        Assert.Null(settings.CreateProxy());
    }

    [Fact(DisplayName = "直连模式绕过每个目标")]
    public void Direct_mode_bypasses_every_destination()
    {
        WebProxy proxy = Assert.IsType<WebProxy>(
            NetworkSettings.From(Section("direct")).ThrowIfError().CreateProxy());

        Assert.Null(proxy.Address);
        Assert.True(proxy.IsBypassed(new Uri("https://api.example.com/v1")));
    }

    [Theory]
    [InlineData("http://127.0.0.1:7890", Label = "HTTP 代理")]
    [InlineData("https://proxy.example.com:8443", Label = "HTTPS 代理")]
    [InlineData("socks4://127.0.0.1:1080", Label = "SOCKS4 代理")]
    [InlineData("socks5://127.0.0.1:1080", Label = "SOCKS5 代理")]
    public void Supported_proxy_addresses_are_accepted(string value)
    {
        NetworkSettings settings = NetworkSettings.From(Section(value)).ThrowIfError();

        Assert.NotNull(settings.CreateProxy());
    }

    [Theory]
    [InlineData("ftp://127.0.0.1:21", Label = "FTP 协议")]
    [InlineData("127.0.0.1:7890", Label = "缺少协议")]
    [InlineData("http://", Label = "缺少主机")]
    [InlineData("none", Label = "无代理")]
    public void Invalid_proxy_values_are_rejected(string value)
    {
        Assert.True(NetworkSettings.From(Section(value)).IsError);
    }

    [Fact(DisplayName = "应用把配置的代理写入进程默认")]
    public void Apply_writes_the_configured_proxy_to_the_process_default()
    {
        IWebProxy original = HttpClient.DefaultProxy;
        try
        {
            NetworkSettings.From(Section("http://127.0.0.1:7890")).ThrowIfError().Apply();

            WebProxy applied = Assert.IsType<WebProxy>(HttpClient.DefaultProxy);

            Assert.Equal(new Uri("http://127.0.0.1:7890"), applied.Address);
        }
        finally
        {
            HttpClient.DefaultProxy = original;
        }
    }

    [Fact(DisplayName = "系统模式下应用保持进程默认")]
    public void Apply_keeps_the_process_default_in_system_mode()
    {
        IWebProxy original = HttpClient.DefaultProxy;

        NetworkSettings.From(Section("system")).ThrowIfError().Apply();

        Assert.Same(original, HttpClient.DefaultProxy);
    }

    private static JsonObject Section(string proxy) =>
        new() { [nameof(NetworkSettings.Proxy)] = proxy };
}