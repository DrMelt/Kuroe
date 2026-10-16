using Kuroe.Cli.Views;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>启动横幅：目录规模、默认流程与缺项指引。</summary>
public sealed class StartupViewTests
{
    [Fact]
    public void Startup_banner_reports_scale_and_next_steps()
    {
        using CliSession cli = new();
        StartupView startup = new(cli.Environment.Catalog, cli.Environment.Models, cli.Environment.Tools, cli.Environment.Flows, cli.Terminal);

        startup.Print();

        Assert.Contains("Kuroe 已启动，当前模型 fake", cli.Output.Output);
        Assert.Contains("流程模板 1 条（默认 默认）", cli.Output.Output);
        Assert.Contains("/task new <目标>", cli.Output.Output);
        Assert.DoesNotContain("先 /provider add", cli.Output.Output);
    }

    [Fact]
    public void Startup_banner_points_to_provider_setup_when_the_catalog_is_empty()
    {
        using CliSession cli = new(seedCatalog: false);
        StartupView startup = new(cli.Environment.Catalog, cli.Environment.Models, cli.Environment.Tools, cli.Environment.Flows, cli.Terminal);

        startup.Print();

        Assert.Contains("当前模型 未选择", cli.Output.Output);
        Assert.Contains("先 /provider add <提供商> <端点> <凭据> 添加提供商", cli.Output.Output);
    }
}
