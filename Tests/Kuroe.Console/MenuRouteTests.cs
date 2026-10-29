using ApiHub.Shared.Models;
using Kuroe.Console.Views;
using Kuroe.Flow.Shared.StaticGraph.Flow;

using Kuroe.Node.Llm;
using Kuroe.Node.Llm.Tools;
using Kuroe.Tool.Command.Json;
using Kuroe.Tool.Json;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>目录、流程、设置与工具菜单的动作与回显。</summary>
public sealed class MenuRouteTests
{
    [Fact]
    public void Provider_add_reports_saved_and_catalog_keeps_it()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.AddProvider("备用", "https://example.invalid/v2", "k");

        Assert.Contains("已保存。", cli.Output.Output);
        Assert.Contains(cli.Environment.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "备用");
    }

    [Fact]
    public void Provider_add_keeps_surrounding_spaces()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.AddProvider("备用 ", "https://example.invalid/v2", "k");

        Assert.Contains("已保存。", cli.Output.Output);
        Assert.Contains(cli.Environment.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "备用 ");
    }

    [Fact]
    public void Provider_add_from_the_menu_shows_every_field_and_the_filled_state()
    {
        using ConsoleSession cli = new();

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("backup");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("https://example.invalid/v2");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("token");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Catalogs.Browse();

        string output = cli.Output.Output;
        Assert.Contains("添加提供商", output);
        Assert.Contains("提供商名称", output);
        Assert.Contains("端点", output);
        Assert.Contains("凭据", output);
        Assert.Contains("已填写", output);
        Assert.DoesNotContain("token", output);
        Assert.Contains(cli.Environment.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "backup");
    }

    [Fact]
    public void Provider_key_replaces_the_credentials()
    {
        using ConsoleSession cli = new();
        ProviderDefinition provider = cli.Environment.Catalog.Snapshot().Providers.Single();

        cli.Catalogs.SetProviderKey(provider, "新凭据");

        Assert.Contains("已保存。", cli.Output.Output);
        Assert.Equal("新凭据", cli.Environment.Catalog.Snapshot().Providers.Single().ApiKey.Value);
    }

    [Fact]
    public void Provider_remove_deletes_an_unreferenced_provider()
    {
        using ConsoleSession cli = new();
        cli.Catalogs.AddProvider("备用", "https://example.invalid/v2", "k");
        ProviderDefinition provider = cli.Environment.Catalog.Snapshot().Providers.Single(entry => entry.ProviderName.Value == "备用");

        cli.Catalogs.RemoveProvider(provider);

        Assert.Contains("已删除。", cli.Output.Output);
        Assert.DoesNotContain(cli.Environment.Catalog.Snapshot().Providers,
            entry => entry.ProviderName.Value == "备用");
    }

    [Fact]
    public void Provider_remove_of_a_referenced_provider_is_rejected()
    {
        using ConsoleSession cli = new();
        ProviderDefinition provider = cli.Environment.Catalog.Snapshot().Providers.Single();

        cli.Catalogs.RemoveProvider(provider);

        Assert.Contains("未生效。", cli.ErrorsOut.Output);
        Assert.Contains(cli.Environment.Catalog.Snapshot().Providers,
            entry => entry.ProviderName.Value == "test");
    }

    [Fact]
    public void Provider_list_renders_the_catalog()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.PrintCatalog();

        Assert.Contains("提供商", cli.Output.Output);
        Assert.Contains("凭据已设置", cli.Output.Output);
        Assert.Contains("当前", cli.Output.Output);
    }

    [Fact]
    public void Provider_list_without_providers_shows_the_setup_hint()
    {
        using ConsoleSession cli = new(seedCatalog: false);

        cli.Catalogs.PrintCatalog();

        Assert.Contains("目录为空", cli.Output.Output);
    }

    [Fact]
    public void Model_add_and_select_go_through_the_catalog()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.AddModel(ModelName.Create("m2").Value, ProviderName.Create("test").Value);
        Assert.Contains("已注册。", cli.Output.Output);

        cli.Catalogs.SelectModel(ModelName.Create("m2").Value);
        Assert.Contains("已保存并生效。", cli.Output.Output);
        Assert.Equal("m2", cli.Environment.Models.Current?.Value);
    }

    [Fact]
    public void Model_add_from_the_menu_shows_the_provider_context()
    {
        using ConsoleSession cli = new();

        for (int index = 0; index < 4; index++)
        {
            cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        }

        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("m2");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Catalogs.Browse();

        string output = cli.Output.Output;
        Assert.Contains("注册模型", output);
        Assert.Contains("提供商", output);
        Assert.Contains("test", output);
        Assert.Contains("已注册。", output);
        Assert.Contains(cli.Environment.Catalog.Snapshot().Models,
            model => model.ModelName.Value == "m2");
    }

    [Fact]
    public void Model_select_of_an_unregistered_model_points_to_the_menu()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.SelectModel(ModelName.Create("缺失").Value);

        Assert.Contains("未生效。", cli.ErrorsOut.Output);
        Assert.Contains("「模型与提供商」", cli.Output.Output);
    }

    [Fact]
    public void Model_unselect_clears_the_selection()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.UnselectModel();

        Assert.Contains("已保存并生效。", cli.Output.Output);
        Assert.Null(cli.Environment.Models.Current);
    }

    [Fact]
    public void Model_remove_of_the_current_model_clears_the_selection()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.RemoveModel(ModelName.Create("fake").Value);

        Assert.Contains("已注销当前模型，选择一并取消。", cli.Output.Output);
        Assert.Null(cli.Environment.Models.Current);
    }

    [Fact]
    public void Catalog_export_writes_a_masked_file()
    {
        using ConsoleSession cli = new();
        string target = Path.Combine(cli.Environment.Root, "export.json");

        cli.Catalogs.Export(target);

        Assert.True(File.Exists(target));
        Assert.Contains("凭据已替换为占位符", cli.Output.Output);
        Assert.DoesNotContain("key", File.ReadAllText(target));
        Assert.Contains("***", File.ReadAllText(target));
    }

    [Fact]
    public void Catalog_import_merges_the_masked_catalog()
    {
        using ConsoleSession cli = new();
        string source = Path.Combine(cli.Environment.Root, "export.json");
        cli.Catalogs.Export(source);

        using ConsoleSession fresh = new(seedCatalog: false);
        fresh.Catalogs.Import(source);

        Assert.Contains("已从", fresh.Output.Output);
        Assert.Equal("test", Assert.Single(fresh.Environment.Catalog.Snapshot().Providers).ProviderName.Value);
    }

    [Fact]
    public void Catalog_menu_shows_the_current_model_and_returns_on_escape()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Catalogs.Browse();

        Assert.Contains("当前模型 fake。", cli.Output.Output);
        Assert.Contains("查看目录", cli.Output.Output);
        Assert.Contains("添加提供商", cli.Output.Output);
    }

    [Fact]
    public void Flow_menu_lists_flows_and_returns_on_escape()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Flows.Browse();

        string output = cli.Output.Output;
        Assert.Contains("流程 1 条", output);
        Assert.Contains("默认", output);
        Assert.Contains("执行节点", output);
        Assert.True(
            output.IndexOf("~ 刷新", StringComparison.Ordinal) < output.IndexOf("≡ 查看流程详情", StringComparison.Ordinal),
            output);
    }

    [Fact]
    public void Flow_detail_renders_the_node_tree()
    {
        using ConsoleSession cli = new();

        cli.Flows.PrintDetail(new FlowName("默认"));

        Assert.Contains("Start", cli.Output.Output);
        Assert.Contains("制定计划", cli.Output.Output);
        Assert.Contains("分配执行", cli.Output.Output);
        Assert.Contains("上游", cli.Output.Output);
        Assert.Contains("├", cli.Output.Output);
        Assert.Contains("└", cli.Output.Output);
    }

    [Fact]
    public void Flow_detail_page_refreshes_and_returns_to_the_flow_menu()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Flows.Browse();

        string output = cli.Output.Output;
        Assert.Equal(2, output.Split("── 主菜单 › 流程 › 默认").Length - 1);
        Assert.Contains("← 返回上一级", output);

        int page = output.IndexOf("── 主菜单 › 流程 › 默认", StringComparison.Ordinal);
        int back = output.IndexOf("← 返回上一级", StringComparison.Ordinal);
        int list = output.IndexOf("流程 1 条", page + 1, StringComparison.Ordinal);
        Assert.True(page >= 0 && back > page && list > back, output);
    }

    [Fact]
    public void Catalog_page_stays_until_back()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Catalogs.Browse();

        Assert.Contains("── 主菜单 › 模型与提供商 › 目录", cli.Output.Output);
        Assert.Contains("← 返回上一级", cli.Output.Output);
    }

    [Fact]
    public void Flow_import_reports_new_flows_and_lists_them()
    {
        using ConsoleSession cli = new();
        string file = Path.Combine(cli.Environment.Root, "extra.json");
        File.WriteAllText(file, ImportedFlow);

        cli.Flows.Import(file);

        Assert.Contains("导入 1 条流程。", cli.Output.Output);
        Assert.Contains("新增流程 备用", cli.Output.Output);

        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Flows.Browse();
        Assert.Contains("备用", cli.Output.Output);
    }

    [Fact]
    public void Flow_import_from_the_menu_reads_the_file_path()
    {
        using ConsoleSession cli = new();
        string file = Path.Combine(cli.Environment.Root, "extra.json");
        File.WriteAllText(file, ImportedFlow);

        // 流程库非空时依次是刷新、查看详情与导入
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter(file);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Flows.Browse();

        Assert.Contains("导入 1 条流程。", cli.Output.Output);
    }

    [Fact]
    public void Tool_view_lists_every_registered_tool()
    {
        using ConsoleSession cli = new();

        cli.Tools.Print();

        string output = cli.Output.Output;
        Assert.Contains("GetLocalTime", output);
        Assert.Contains("file", output);
        Assert.Contains("git", output);
        Assert.Contains("KuroeInfo", output);
        Assert.Contains("source", output);
        Assert.Contains("├", output);
    }

    [Fact]
    public void Tool_view_renders_a_configured_command_tool()
    {
        using ConsoleSession cli = new();
        ConfiguredTool tool = Configured();

        new ToolView(ToolCollection.ForDisplay([tool.Definition], new ToolJson()), [tool], cli.Terminal).Print();

        string output = cli.Output.Output;
        Assert.Contains("模板", output);
        Assert.Contains("RunDiff", output);
        Assert.Contains("git diff {path}", output);
        Assert.Contains("path*", output);
    }

    [Fact]
    public void Tool_view_groups_tools_by_path_segments()
    {
        using ConsoleSession cli = new();
        IReadOnlyList<ConfiguredTool> tools = Configured("""
            {
              "Tools": [
                { "Name": "Run", "Group": "ops/read", "Template": ["echo", "run"] },
                { "Name": "Stop", "Group": "ops/read", "Template": ["echo", "stop"] },
                { "Name": "Flat", "Template": ["echo", "flat"] }
              ]
            }
            """);

        new ToolView(
            ToolCollection.ForDisplay(tools.Select(tool => tool.Definition), new ToolJson()),
            tools,
            cli.Terminal).Print();

        string output = cli.Output.Output;
        string[] lines = output.Replace("\r", string.Empty).Split('\n');
        int ops = Array.FindIndex(lines, line => line.Contains("ops", StringComparison.Ordinal));
        int read = Array.FindIndex(lines, line => line.Contains("read", StringComparison.Ordinal));
        int run = Array.FindIndex(lines, line => line.Contains("Run", StringComparison.Ordinal));
        int stop = Array.FindIndex(lines, line => line.Contains("Stop", StringComparison.Ordinal));
        int flat = Array.FindIndex(lines, line => line.Contains("Flat", StringComparison.Ordinal));

        Assert.Equal(1, output.Split("ops").Length - 1);
        Assert.Contains("├", output);
        Assert.Contains("└", output);
        Assert.True(ops >= 0 && read > ops && run > read && stop > run && flat > stop, output);
    }

    [Fact]
    public void Settings_set_writes_and_echoes_the_effect()
    {
        using ConsoleSession cli = new();

        cli.Settings.Set("Runtime:LogLevel", "Debug");

        Assert.Contains("已保存，重启后生效。", cli.Output.Output);
        Assert.Equal("Debug", cli.Environment.Settings.TryGetUserValue("Runtime:LogLevel"));
    }

    [Fact]
    public void Settings_set_language_points_to_the_reset_entry()
    {
        using ConsoleSession cli = new();

        cli.Settings.Set("Assistant:Language", "English");

        Assert.Contains("在「Kuroe 助手」页选择「重置对话」后生效。", cli.Output.Output);
    }

    [Fact]
    public void Settings_set_with_an_unknown_path_is_rejected()
    {
        using ConsoleSession cli = new();

        cli.Settings.Set("Runtime:Nope", "1");

        Assert.Contains("未生效。", cli.ErrorsOut.Output);
    }

    [Fact]
    public void Settings_unset_reports_a_path_absent_from_the_user_layer()
    {
        using ConsoleSession cli = new();

        cli.Settings.Unset("Runtime:LogLevel");

        Assert.Contains("Runtime:LogLevel 未在用户层设置。", cli.Output.Output);
    }

    [Fact]
    public void Settings_unset_clears_a_user_layer_value()
    {
        using ConsoleSession cli = new();
        cli.Settings.Set("Runtime:LogLevel", "Debug");

        cli.Settings.Unset("Runtime:LogLevel");

        Assert.Null(cli.Environment.Settings.TryGetUserValue("Runtime:LogLevel"));
    }

    [Fact]
    public void Settings_unset_with_an_unknown_path_is_rejected()
    {
        using ConsoleSession cli = new();

        cli.Settings.Unset("Runtime:Nope");

        Assert.Contains("未生效。", cli.ErrorsOut.Output);
    }

    [Fact]
    public void Settings_config_marks_entries_from_the_user_layer()
    {
        using ConsoleSession cli = new();
        cli.Settings.PrintConfig();
        Assert.Contains("用户层文件：", cli.Output.Output);
        Assert.Contains("用户层未设置", cli.Output.Output);

        cli.Settings.Set("Runtime:LogLevel", "Debug");
        cli.Settings.PrintConfig();

        Assert.Contains("用户层已设置", cli.Output.Output);
    }

    [Fact]
    public void Settings_menu_lists_entries_and_returns_on_escape()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Settings.Browse();

        string output = cli.Output.Output;
        Assert.Contains("Runtime:LogLevel", output);
        Assert.Contains("用户层未设置", output);
        Assert.True(
            output.IndexOf("~ 刷新", StringComparison.Ordinal) < output.IndexOf("Runtime:LogLevel · ", StringComparison.Ordinal),
            output);
    }

    [Fact]
    public void Settings_menu_writes_a_new_value_for_the_selected_entry()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("Debug");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Settings.Browse();

        Assert.Contains("已保存，重启后生效。", cli.Output.Output);
        Assert.Contains("写入设置", cli.Output.Output);
        Assert.Contains("当前值", cli.Output.Output);
        Assert.Equal("Debug", cli.Environment.Settings.TryGetUserValue("Runtime:LogLevel"));
    }

    /// <summary>经命令工具文件层装载一条 RunDiff，取回配置工具的载体。</summary>
    private static ConfiguredTool Configured() =>
        Configured("""
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
            """)[0];

    /// <summary>把给出的命令工具文件内容写到临时目录并装载，取回配置工具记录。</summary>
    private static IReadOnlyList<ConfiguredTool> Configured(string json)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(Path.Combine(directory, "tools.json"), json);

            return new CommandToolFile().Load(directory, Path.GetTempPath()).Value;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>导入用的流程文件：与基线库无交叉，可独立装配。</summary>
    private const string ImportedFlow = """
        {
          "Flows": [
            {
              "Name": "备用",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "唯一步骤", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}