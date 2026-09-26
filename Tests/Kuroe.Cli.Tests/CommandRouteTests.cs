using Kuroe.Cli.Commands;
using Kuroe.Cli.Views;
using Kuroe.TestSupport;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>目录、模型、流程与偏好命令族的分发与对库的调用回显。</summary>
public sealed class CommandRouteTests
{
    [Fact]
    public void Provider_add_reports_saved_and_catalog_keeps_it()
    {
        using Ui ui = new();
        ProviderCommands commands = new(ui.Harness.Catalog, ui.Printer, ui.Terminal, ui.Results);
        commands.Run(["/provider", "add", "备用", "https://example.invalid/v2", "k"]);

        Assert.Contains("已保存。", ui.Output.Output);
        Assert.Contains(ui.Harness.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "备用");
    }

    [Fact]
    public void Provider_with_wrong_argument_count_shows_usage()
    {
        using Ui ui = new();
        ui.Providers.Run(["/provider", "add", "备用"]);

        Assert.Contains("用法：/provider", ui.Output.Output);
    }

    [Fact]
    public void Model_add_and_select_go_through_the_catalog()
    {
        using Ui ui = new();
        ui.Models.Run(["/model", "add", "m2", "test"]);
        Assert.Contains("已注册。", ui.Output.Output);

        ui.Models.Run(["/model", "m2"]);
        Assert.Contains("已保存并生效。", ui.Output.Output);
        Assert.Equal("m2", ui.Harness.Models.Current);
    }

    [Fact]
    public void Model_none_clears_the_selection()
    {
        using Ui ui = new();
        ui.Models.Run(["/model", "none"]);

        Assert.Contains("已保存并生效。", ui.Output.Output);
        Assert.Null(ui.Harness.Models.Current);
    }

    [Fact]
    public void Catalog_export_writes_a_masked_file()
    {
        using Ui ui = new();
        string target = Path.Combine(ui.Harness.Root, "export.json");

        ui.Catalogs.Run(["/catalog", "export", target]);

        Assert.True(File.Exists(target));
        Assert.Contains("凭据已替换为占位符", ui.Output.Output);
        Assert.DoesNotContain("sk-", File.ReadAllText(target));
        Assert.Contains("***", File.ReadAllText(target));
    }

    [Fact]
    public void Catalog_import_merges_the_masked_catalog()
    {
        using Ui ui = new();
        string source = Path.Combine(ui.Harness.Root, "export.json");
        ui.Catalogs.Run(["/catalog", "export", source]);

        using Ui fresh = new(seedCatalog: false);
        fresh.Catalogs.Run(["/catalog", "import", source]);

        Assert.Contains("已从", fresh.Output.Output);
        Assert.Equal("test", Assert.Single(fresh.Harness.Catalog.Snapshot().Providers).ProviderName.Value);
    }

    [Fact]
    public void Flow_list_and_show_render_steps()
    {
        using Ui ui = new();
        ui.Flows.Run(["/flow", "list"]);
        Assert.Contains("默认流程", ui.Output.Output);
        Assert.Contains("制定计划", ui.Output.Output);

        ui.Flows.Run(["/flow", "show", "默认"]);
        Assert.Contains("制定计划", ui.Output.Output);
        Assert.Contains("分配执行", ui.Output.Output);
        Assert.Contains("整体检查", ui.Output.Output);
    }

    [Fact]
    public void Flow_default_writes_the_user_layer()
    {
        using Ui ui = new();
        ui.Flows.Run(["/flow", "default", "默认"]);

        Assert.Contains("已保存并生效。", ui.Output.Output);
        Assert.Equal("默认", ui.Harness.Flows.DefaultName);
    }

    [Fact]
    public void Settings_set_writes_and_echoes_the_effect()
    {
        using Ui ui = new();
        ui.Settings.Set(["/set", "Agent:Temperature", "0.5"]);

        Assert.Contains("已保存并生效。", ui.Output.Output);
        Assert.Equal("0.5", ui.Harness.Settings.TryGetUserValue("Agent:Temperature"));
    }

    [Fact]
    public void Settings_set_with_wrong_argument_count_shows_usage()
    {
        using Ui ui = new();
        ui.Settings.Set(["/set", "Agent:Temperature"]);

        Assert.Contains("用法：/set", ui.Output.Output);
    }
}