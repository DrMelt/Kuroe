using Kuroe.Console.Views;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>启动横幅：目录规模与缺项指引。</summary>
public sealed class StartupViewTests
{
    [Fact]
    public void Startup_banner_reports_scale_and_next_steps()
    {
        using ConsoleSession cli = new();
        StartupView startup = new(cli.Environment.Catalog, cli.Environment.Models, cli.Environment.Tools, cli.Environment.Flows, cli.Terminal);

        startup.Print();

        Assert.Contains("已启动，当前模型 fake", cli.Output.Output);
        Assert.Contains("流程模板 1 条。", cli.Output.Output);
        Assert.Contains("/task new --flow <流程> <目标>", cli.Output.Output);
        string top = cli.Output.Output.Replace("\r", string.Empty).Split('\n')[0];
        Assert.Matches("^┌─+┐$", top);
        Assert.DoesNotContain("先 /provider add", cli.Output.Output);
    }

    [Fact]
    public void Startup_banner_points_to_provider_setup_when_the_catalog_is_empty()
    {
        using ConsoleSession cli = new(seedCatalog: false);
        StartupView startup = new(cli.Environment.Catalog, cli.Environment.Models, cli.Environment.Tools, cli.Environment.Flows, cli.Terminal);

        startup.Print();

        Assert.Contains("当前模型 未选择", cli.Output.Output);
        Assert.Contains("先 /provider add <提供商> <端点> <凭据> 添加提供商", cli.Output.Output);
    }
}
