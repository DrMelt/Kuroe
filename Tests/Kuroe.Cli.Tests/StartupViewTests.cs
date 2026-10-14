using Kuroe.Cli.Views;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>启动横幅：目录规模、默认流程与缺项指引。</summary>
public sealed class StartupViewTests
{
    [Fact]
    public void Startup_banner_reports_scale_and_next_steps()
    {
        using Ui ui = new();
        StartupView startup = new(ui.Harness.Catalog, ui.Harness.Models, ui.Harness.Tools, ui.Harness.Flows, ui.Terminal);

        startup.Print();

        Assert.Contains("Kuroe 已启动，当前模型 fake", ui.Output.Output);
        Assert.Contains("流程模板 1 条（默认 默认）", ui.Output.Output);
        Assert.Contains("/task new <目标>", ui.Output.Output);
        Assert.DoesNotContain("先 /provider add", ui.Output.Output);
    }

    [Fact]
    public void Startup_banner_points_to_provider_setup_when_the_catalog_is_empty()
    {
        using Ui ui = new(seedCatalog: false);
        StartupView startup = new(ui.Harness.Catalog, ui.Harness.Models, ui.Harness.Tools, ui.Harness.Flows, ui.Terminal);

        startup.Print();

        Assert.Contains("当前模型 未选择", ui.Output.Output);
        Assert.Contains("先 /provider add <提供商> <端点> <凭据> 添加提供商", ui.Output.Output);
    }
}
