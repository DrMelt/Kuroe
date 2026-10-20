using Kuroe.Cli.Commands;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Tool;
using Kuroe.Tool.Command.Json;
using Kuroe.Tool.Json;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>目录、模型、流程与偏好命令族的分发与对库的调用回显。</summary>
public sealed class CommandRouteTests
{
    [Fact]
    public void Provider_add_reports_saved_and_catalog_keeps_it()
    {
        using CliSession cli = new();
        ProviderCommands commands = new(cli.Environment.Catalog, cli.Printer, cli.Terminal, cli.Results);
        commands.Run(["/provider", "add", "备用", "https://example.invalid/v2", "k"]);

        Assert.Contains("已保存。", cli.Output.Output);
        Assert.Contains(cli.Environment.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "备用");
    }

    [Fact]
    public void Provider_add_keeps_surrounding_spaces()
    {
        using CliSession cli = new();
        cli.Providers.Run(["/provider", "add", "备用 ", "https://example.invalid/v2", "k"]);

        Assert.Contains("已保存。", cli.Output.Output);
        Assert.Contains(cli.Environment.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "备用 ");
    }

    [Fact]
    public void Provider_with_wrong_argument_count_shows_usage()
    {
        using CliSession cli = new();
        cli.Providers.Run(["/provider", "add", "备用"]);

        Assert.Contains("用法：/provider", cli.Output.Output);
    }

    [Fact]
    public void Model_add_and_select_go_through_the_catalog()
    {
        using CliSession cli = new();
        cli.Models.Run(["/model", "add", "m2", "test"]);
        Assert.Contains("已注册。", cli.Output.Output);

        cli.Models.Run(["/model", "m2"]);
        Assert.Contains("已保存并生效。", cli.Output.Output);
        Assert.Equal("m2", cli.Environment.Models.Current?.Value);
    }

    [Fact]
    public void Model_add_and_select_normalize_surrounding_spaces()
    {
        using CliSession cli = new();
        cli.Models.Run(["/model", "add", "m2 ", "test "]);
        Assert.Contains("已注册。", cli.Output.Output);

        cli.Models.Run(["/model", " m2 "]);
        Assert.Equal("m2", cli.Environment.Models.Current?.Value);
    }

    [Fact]
    public void Model_select_with_blank_name_is_rejected()
    {
        using CliSession cli = new();
        cli.Models.Run(["/model", "   "]);

        Assert.Contains("错误：ModelName.Invalid：模型名不能为空。", cli.ErrorsOut.Output);
        Assert.Contains("未生效。", cli.ErrorsOut.Output);
    }

    [Fact]
    public void Model_remove_with_surrounding_spaces_removes_the_selection()
    {
        using CliSession cli = new();
        cli.Models.Run(["/model", "rm", " fake "]);

        Assert.Contains("已注销当前模型，选择一并取消。", cli.Output.Output);
        Assert.Null(cli.Environment.Models.Current);
    }

    [Fact]
    public void Model_none_clears_the_selection()
    {
        using CliSession cli = new();
        cli.Models.Run(["/model", "none"]);

        Assert.Contains("已保存并生效。", cli.Output.Output);
        Assert.Null(cli.Environment.Models.Current);
    }

    [Fact]
    public void Catalog_export_writes_a_masked_file()
    {
        using CliSession cli = new();
        string target = Path.Combine(cli.Environment.Root, "export.json");

        cli.Catalogs.Run(["/catalog", "export", target]);

        Assert.True(File.Exists(target));
        Assert.Contains("凭据已替换为占位符", cli.Output.Output);
        Assert.DoesNotContain("sk-", File.ReadAllText(target));
        Assert.Contains("***", File.ReadAllText(target));
    }

    [Fact]
    public void Catalog_import_merges_the_masked_catalog()
    {
        using CliSession cli = new();
        string source = Path.Combine(cli.Environment.Root, "export.json");
        cli.Catalogs.Run(["/catalog", "export", source]);

        using CliSession fresh = new(seedCatalog: false);
        fresh.Catalogs.Run(["/catalog", "import", source]);

        Assert.Contains("已从", fresh.Output.Output);
        Assert.Equal("test", Assert.Single(fresh.Environment.Catalog.Snapshot().Providers).ProviderName.Value);
    }

    [Fact]
    public void Flow_list_and_show_render_steps()
    {
        using CliSession cli = new();
        cli.Flows.Run(["/flow", "list"]);
        Assert.Contains("制定计划", cli.Output.Output);

        cli.Flows.Run(["/flow", "show", "默认"]);
        Assert.Contains("制定计划", cli.Output.Output);
        Assert.Contains("分配执行", cli.Output.Output);
    }

    [Fact]
    public void Tool_list_reports_no_configured_command_tools_by_default()
    {
        using CliSession cli = new();
        new ToolCommands(cli.Environment.ConfiguredTools, cli.Terminal).Run(["/tool", "list"]);

        Assert.Contains("没有配置命令工具", cli.Output.Output);
    }

    [Fact]
    public void Tool_list_with_wrong_argument_count_shows_usage()
    {
        using CliSession cli = new();
        new ToolCommands(cli.Environment.ConfiguredTools, cli.Terminal).Run(["/tool"]);

        Assert.Contains("用法：/tool", cli.Output.Output);
    }

    [Fact]
    public void Settings_set_writes_and_echoes_the_effect()
    {
        using CliSession cli = new();
        cli.Settings.Set(["/set", "Runtime:Temperature", "0.5"]);

        Assert.Contains("已保存并生效。", cli.Output.Output);
        Assert.Equal("0.5", cli.Environment.Settings.TryGetUserValue("Runtime:Temperature"));
    }

    [Fact]
    public void Settings_set_language_hints_dialogue_rebuild()
    {
        using CliSession cli = new();
        cli.Settings.Set(["/set", "Assistant:Language", "English"]);

        Assert.Contains("内置助手语言已变更，已有对话任务需用 /reset 重建后生效。", cli.Output.Output);
    }

    [Fact]
    public void Settings_set_with_wrong_argument_count_shows_usage()
    {
        using CliSession cli = new();
        cli.Settings.Set(["/set", "Runtime:Temperature"]);

        Assert.Contains("用法：/set", cli.Output.Output);
    }

    [Fact]
    public void Provider_list_renders_the_catalog()
    {
        using CliSession cli = new();
        cli.Providers.Run(["/provider", "list"]);

        Assert.Contains("提供商：", cli.Output.Output);
        Assert.Contains("凭据已设置", cli.Output.Output);
    }

    [Fact]
    public void Provider_list_without_providers_shows_the_setup_hint()
    {
        using CliSession cli = new(seedCatalog: false);
        cli.Providers.Run(["/provider", "list"]);

        Assert.Contains("目录为空", cli.Output.Output);
    }

    [Fact]
    public void Provider_key_replaces_the_credentials()
    {
        using CliSession cli = new();
        cli.Providers.Run(["/provider", "key", "test", "新凭据"]);

        Assert.Contains("已保存。", cli.Output.Output);
        Assert.Equal("新凭据", cli.Environment.Catalog.Snapshot().Providers.Single().ApiKey.Value);
    }

    [Fact]
    public void Provider_remove_deletes_an_unreferenced_provider()
    {
        using CliSession cli = new();
        cli.Providers.Run(["/provider", "add", "备用", "https://example.invalid/v2", "k"]);

        cli.Providers.Run(["/provider", "rm", "备用"]);

        Assert.Contains("已删除。", cli.Output.Output);
        Assert.DoesNotContain(cli.Environment.Catalog.Snapshot().Providers, provider => provider.ProviderName.Value == "备用");
    }

    [Fact]
    public void Provider_remove_of_a_referenced_provider_is_rejected()
    {
        using CliSession cli = new();

        cli.Providers.Run(["/provider", "rm", "test"]);

        Assert.Contains("未生效。", cli.ErrorsOut.Output);
        Assert.Contains(cli.Environment.Catalog.Snapshot().Providers, provider => provider.ProviderName.Value == "test");
    }

    [Fact]
    public void Model_bare_command_prints_the_current_model()
    {
        using CliSession cli = new();
        cli.Models.Run(["/model"]);

        Assert.Contains("当前模型 fake。", cli.Output.Output);
    }

    [Fact]
    public void Model_list_renders_the_registered_models()
    {
        using CliSession cli = new();
        cli.Models.Run(["/model", "list"]);

        Assert.Contains("模型：", cli.Output.Output);
        Assert.Contains("→ test", cli.Output.Output);
    }

    [Fact]
    public void Catalog_list_renders_providers_and_models()
    {
        using CliSession cli = new();
        cli.Catalogs.Run(["/catalog", "list"]);

        Assert.Contains("提供商：", cli.Output.Output);
        Assert.Contains("模型：", cli.Output.Output);
    }

    [Fact]
    public void Flow_add_imports_flows_from_a_file()
    {
        using CliSession cli = new();
        string file = Path.Combine(cli.Environment.Root, "extra.json");
        File.WriteAllText(file, ImportedFlow);

        cli.Flows.Run(["/flow", "add", file]);

        Assert.Contains("导入 1 条流程。", cli.Output.Output);
        Assert.Contains("新增流程 备用", cli.Output.Output);

        cli.Flows.Run(["/flow", "list"]);
        Assert.Contains("备用", cli.Output.Output);
    }

    [Fact]
    public void Settings_unset_reports_a_path_absent_from_the_user_layer()
    {
        using CliSession cli = new();
        cli.Settings.Unset(["/unset", "Runtime:Temperature"]);

        Assert.Contains("Runtime:Temperature 未在用户层设置。", cli.Output.Output);
    }

    [Fact]
    public void Settings_unset_clears_a_user_layer_value()
    {
        using CliSession cli = new();
        cli.Settings.Set(["/set", "Runtime:Temperature", "0.5"]);

        cli.Settings.Unset(["/unset", "Runtime:Temperature"]);

        Assert.Null(cli.Environment.Settings.TryGetUserValue("Runtime:Temperature"));
    }

    [Fact]
    public void Settings_unset_with_an_unknown_path_is_rejected()
    {
        using CliSession cli = new();
        cli.Settings.Unset(["/unset", "Runtime:Nope"]);

        Assert.Contains("未生效。", cli.ErrorsOut.Output);
    }

    [Fact]
    public void Settings_config_marks_entries_from_the_user_layer()
    {
        using CliSession cli = new();
        cli.Settings.PrintConfig();
        Assert.Contains("用户层文件：", cli.Output.Output);
        Assert.Contains("用户层未设置", cli.Output.Output);

        cli.Settings.Set(["/set", "Runtime:Temperature", "0.5"]);
        cli.Settings.PrintConfig();

        Assert.Contains("用户层已设置", cli.Output.Output);
    }

    [Fact]
    public void Tool_list_renders_the_configured_command_tools()
    {
        using CliSession cli = new();

        new ToolCommands([Configured()], cli.Terminal).Run(["/tool", "list"]);

        Assert.Contains("命令工具：", cli.Output.Output);
        Assert.Contains("RunDiff", cli.Output.Output);
        Assert.Contains("git diff {path}", cli.Output.Output);
        Assert.Contains("path*", cli.Output.Output);
    }

    /// <summary>经命令工具文件层装载一条 RunDiff，取回配置工具的载体。</summary>
    private static ConfiguredTool Configured()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "tools.json"), """
            {
              "Tools": [
                {
                  "Name": "RunDiff",
                  "Group": "git",
                  "Description": "查看指定文件相对 HEAD 的改动。",
                  "Template": ["git", "diff", "{path}"],
                  "Parameters": [ { "Name": "path", "Required": true } ]
                }
              ]
            }
            """);

        return new CommandToolFile().Load(directory, Path.GetTempPath()).Value[0];
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
                    { "Name": "唯一步骤", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}
