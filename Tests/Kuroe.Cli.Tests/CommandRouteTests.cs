using Kuroe.Cli.Commands;
using Kuroe.Cli.Views;
using Kuroe.Flows.Shared.StaticGraph;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Workflows.Tools.Command.Model;
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
    public void Provider_add_keeps_surrounding_spaces()
    {
        using Ui ui = new();
        ui.Providers.Run(["/provider", "add", "备用 ", "https://example.invalid/v2", "k"]);

        Assert.Contains("已保存。", ui.Output.Output);
        Assert.Contains(ui.Harness.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "备用 ");
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
        Assert.Equal("m2", ui.Harness.Models.Current?.Value);
    }

    [Fact]
    public void Model_add_and_select_normalize_surrounding_spaces()
    {
        using Ui ui = new();
        ui.Models.Run(["/model", "add", "m2 ", "test "]);
        Assert.Contains("已注册。", ui.Output.Output);

        ui.Models.Run(["/model", " m2 "]);
        Assert.Equal("m2", ui.Harness.Models.Current?.Value);
    }

    [Fact]
    public void Model_select_with_blank_name_is_rejected()
    {
        using Ui ui = new();
        ui.Models.Run(["/model", "   "]);

        Assert.Contains("错误：ModelName.Invalid：模型名不能为空。", ui.ErrorsOut.Output);
        Assert.Contains("未生效。", ui.ErrorsOut.Output);
    }

    [Fact]
    public void Model_remove_with_surrounding_spaces_removes_the_selection()
    {
        using Ui ui = new();
        ui.Models.Run(["/model", "rm", " fake "]);

        Assert.Contains("已注销当前模型，选择一并取消。", ui.Output.Output);
        Assert.Null(ui.Harness.Models.Current);
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
        Assert.Contains("制定计划", ui.Output.Output);

        ui.Flows.Run(["/flow", "show", "默认"]);
        Assert.Contains("制定计划", ui.Output.Output);
        Assert.Contains("分配执行", ui.Output.Output);
    }

    [Fact]
    public void Flow_default_writes_the_user_layer()
    {
        using Ui ui = new();
        ui.Flows.Run(["/flow", "default", "默认"]);

        Assert.Contains("已保存并生效。", ui.Output.Output);
        Assert.Equal(new FlowName("默认"), ui.Harness.Flows.DefaultName);
    }

    [Fact]
    public void Tool_list_reports_no_configured_command_tools_by_default()
    {
        using Ui ui = new();
        new ToolCommands(ui.Harness.CommandTools, ui.Terminal).Run(["/tool", "list"]);

        Assert.Contains("没有配置命令工具", ui.Output.Output);
    }

    [Fact]
    public void Tool_list_with_wrong_argument_count_shows_usage()
    {
        using Ui ui = new();
        new ToolCommands(ui.Harness.CommandTools, ui.Terminal).Run(["/tool"]);

        Assert.Contains("用法：/tool", ui.Output.Output);
    }

    [Fact]
    public void Settings_set_writes_and_echoes_the_effect()
    {
        using Ui ui = new();
        ui.Settings.Set(["/set", "Runtime:Temperature", "0.5"]);

        Assert.Contains("已保存并生效。", ui.Output.Output);
        Assert.Equal("0.5", ui.Harness.Settings.TryGetUserValue("Runtime:Temperature"));
    }

    [Fact]
    public void Settings_set_with_wrong_argument_count_shows_usage()
    {
        using Ui ui = new();
        ui.Settings.Set(["/set", "Runtime:Temperature"]);

        Assert.Contains("用法：/set", ui.Output.Output);
    }

    [Fact]
    public void Provider_list_renders_the_catalog()
    {
        using Ui ui = new();
        ui.Providers.Run(["/provider", "list"]);

        Assert.Contains("提供商：", ui.Output.Output);
        Assert.Contains("凭据已设置", ui.Output.Output);
    }

    [Fact]
    public void Provider_list_without_providers_shows_the_setup_hint()
    {
        using Ui ui = new(seedCatalog: false);
        ui.Providers.Run(["/provider", "list"]);

        Assert.Contains("目录为空", ui.Output.Output);
    }

    [Fact]
    public void Provider_key_replaces_the_credentials()
    {
        using Ui ui = new();
        ui.Providers.Run(["/provider", "key", "test", "新凭据"]);

        Assert.Contains("已保存。", ui.Output.Output);
        Assert.Equal("新凭据", ui.Harness.Catalog.Snapshot().Providers.Single().ApiKey.Value);
    }

    [Fact]
    public void Provider_remove_deletes_an_unreferenced_provider()
    {
        using Ui ui = new();
        ui.Providers.Run(["/provider", "add", "备用", "https://example.invalid/v2", "k"]);

        ui.Providers.Run(["/provider", "rm", "备用"]);

        Assert.Contains("已删除。", ui.Output.Output);
        Assert.DoesNotContain(ui.Harness.Catalog.Snapshot().Providers, provider => provider.ProviderName.Value == "备用");
    }

    [Fact]
    public void Provider_remove_of_a_referenced_provider_is_rejected()
    {
        using Ui ui = new();

        ui.Providers.Run(["/provider", "rm", "test"]);

        Assert.Contains("未生效。", ui.ErrorsOut.Output);
        Assert.Contains(ui.Harness.Catalog.Snapshot().Providers, provider => provider.ProviderName.Value == "test");
    }

    [Fact]
    public void Model_bare_command_prints_the_current_model()
    {
        using Ui ui = new();
        ui.Models.Run(["/model"]);

        Assert.Contains("当前模型 fake。", ui.Output.Output);
    }

    [Fact]
    public void Model_list_renders_the_registered_models()
    {
        using Ui ui = new();
        ui.Models.Run(["/model", "list"]);

        Assert.Contains("模型：", ui.Output.Output);
        Assert.Contains("→ test", ui.Output.Output);
    }

    [Fact]
    public void Catalog_list_renders_providers_and_models()
    {
        using Ui ui = new();
        ui.Catalogs.Run(["/catalog", "list"]);

        Assert.Contains("提供商：", ui.Output.Output);
        Assert.Contains("模型：", ui.Output.Output);
    }

    [Fact]
    public void Flow_add_imports_flows_from_a_file()
    {
        using Ui ui = new();
        string file = Path.Combine(ui.Harness.Root, "extra.json");
        File.WriteAllText(file, ImportedFlow);

        ui.Flows.Run(["/flow", "add", file]);

        Assert.Contains("导入 1 条流程。", ui.Output.Output);
        Assert.Contains("新增流程 备用", ui.Output.Output);

        ui.Flows.Run(["/flow", "list"]);
        Assert.Contains("备用", ui.Output.Output);
    }

    [Fact]
    public void Settings_unset_reports_a_path_absent_from_the_user_layer()
    {
        using Ui ui = new();
        ui.Settings.Unset(["/unset", "Runtime:Temperature"]);

        Assert.Contains("Runtime:Temperature 未在用户层设置。", ui.Output.Output);
    }

    [Fact]
    public void Settings_unset_clears_a_user_layer_value()
    {
        using Ui ui = new();
        ui.Settings.Set(["/set", "Runtime:Temperature", "0.5"]);

        ui.Settings.Unset(["/unset", "Runtime:Temperature"]);

        Assert.Null(ui.Harness.Settings.TryGetUserValue("Runtime:Temperature"));
    }

    [Fact]
    public void Settings_unset_with_an_unknown_path_is_rejected()
    {
        using Ui ui = new();
        ui.Settings.Unset(["/unset", "Runtime:Nope"]);

        Assert.Contains("未生效。", ui.ErrorsOut.Output);
    }

    [Fact]
    public void Settings_config_marks_entries_from_the_user_layer()
    {
        using Ui ui = new();
        ui.Settings.PrintConfig();
        Assert.Contains("用户层文件：", ui.Output.Output);
        Assert.Contains("用户层未设置", ui.Output.Output);

        ui.Settings.Set(["/set", "Runtime:Temperature", "0.5"]);
        ui.Settings.PrintConfig();

        Assert.Contains("用户层已设置", ui.Output.Output);
    }

    [Fact]
    public void Tool_list_renders_the_configured_command_tools()
    {
        using Ui ui = new();
        CommandToolDefinition diff = new(
            new ToolDefinition(
                new ToolName("RunDiff"),
                "查看指定文件相对 HEAD 的改动。",
                [new ToolParameter(new ToolName("path"), "要查看的文件路径", Required: true)],
                new ToolPath("git")),
            [new CommandToolTemplateItem("git", null), new CommandToolTemplateItem("diff", null), new CommandToolTemplateItem("{path}", null)],
            null,
            120,
            8000);

        new ToolCommands([diff], ui.Terminal).Run(["/tool", "list"]);

        Assert.Contains("命令工具：", ui.Output.Output);
        Assert.Contains("RunDiff", ui.Output.Output);
        Assert.Contains("git diff {path}", ui.Output.Output);
    }

    /// <summary>导入用的流程文件：与基线库无交叉，可独立装配。</summary>
    private const string ImportedFlow = """
        {
          "Flows": [
            {
              "Name": "备用",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "唯一步骤", "Kind": "Executable", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}
