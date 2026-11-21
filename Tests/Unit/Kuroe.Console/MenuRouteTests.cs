using Kuroe.Runner.Llm;
using ApiHub.Shared.Models;
using Kuroe.Console.Views;

using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Runner.Llm.Tools;
using Kuroe.Tool.Implementation.Command.Json;
using Kuroe.Tool.Json;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>目录、流程、设置与工具菜单的动作与回显。</summary>
public sealed class MenuRouteTests
{
    [Fact(DisplayName = "添加提供方报告已保存且目录保留")]
    public void Provider_add_reports_saved_and_catalog_keeps_it()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.AddProvider("备用", "https://example.invalid/v2", "k");

        Assert.Contains("已保存。", cli.Output.Output);
        Assert.Contains(cli.Environment.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "备用");
    }

    [Fact(DisplayName = "添加提供方保留两侧空格")]
    public void Provider_add_keeps_surrounding_spaces()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.AddProvider("备用 ", "https://example.invalid/v2", "k");

        Assert.Contains("已保存。", cli.Output.Output);
        Assert.Contains(cli.Environment.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "备用 ");
    }

    [Fact(DisplayName = "菜单添加提供方显示全部字段与已填状态")]
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

    [Fact(DisplayName = "菜单提供方密钥替换凭据")]
    public void Provider_key_replaces_the_credentials()
    {
        using ConsoleSession cli = new();
        ProviderDefinition provider = cli.Environment.Catalog.Snapshot().Providers.Single();

        cli.Catalogs.SetProviderKey(provider, "新凭据");

        Assert.Contains("已保存。", cli.Output.Output);
        Assert.Equal("新凭据", cli.Environment.Catalog.Snapshot().Providers.Single().ApiKey.Value);
    }

    [Fact(DisplayName = "删除未被引用的提供方")]
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

    [Fact(DisplayName = "删除被引用的提供方被拒绝")]
    public void Provider_remove_of_a_referenced_provider_is_rejected()
    {
        using ConsoleSession cli = new();
        ProviderDefinition provider = cli.Environment.Catalog.Snapshot().Providers.Single();

        cli.Catalogs.RemoveProvider(provider);

        Assert.Contains("未生效。", cli.ErrorsOut.Output);
        Assert.Contains(cli.Environment.Catalog.Snapshot().Providers,
            entry => entry.ProviderName.Value == "test");
    }

    [Fact(DisplayName = "提供方清单渲染目录")]
    public void Provider_list_renders_the_catalog()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.PrintCatalog();

        Assert.Contains("提供商", cli.Output.Output);
        Assert.Contains("凭据已设置", cli.Output.Output);
        Assert.Contains("当前", cli.Output.Output);
    }

    [Fact(DisplayName = "没有提供方时清单显示设置提示")]
    public void Provider_list_without_providers_shows_the_setup_hint()
    {
        using ConsoleSession cli = new(seedCatalog: false);

        cli.Catalogs.PrintCatalog();

        Assert.Contains("目录为空", cli.Output.Output);
    }

    [Fact(DisplayName = "添加与选择模型经由目录")]
    public void Model_add_and_select_go_through_the_catalog()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.AddModel("m2", "test", "m2");
        Assert.Contains("已注册。", cli.Output.Output);

        cli.Catalogs.SelectModel(ModelName.Create("m2").Value);
        Assert.Contains("已保存并生效。", cli.Output.Output);
        Assert.Equal("m2", cli.Environment.Models.Current?.Value);
    }

    [Fact(DisplayName = "菜单添加模型显示提供方上下文")]
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

    [Fact(DisplayName = "菜单添加模型收集提供方模型名")]
    public void Model_add_from_the_menu_collects_the_provider_model_name()
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
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("upstream");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Catalogs.Browse();

        Assert.Contains("已注册。", cli.Output.Output);
        Assert.Contains(cli.Environment.Catalog.Snapshot().Models,
            model => model.ModelName.Value == "m2" && model.ProviderModelName.Value == "upstream");
    }

    [Fact(DisplayName = "选择未注册模型指向菜单")]
    public void Model_select_of_an_unregistered_model_points_to_the_menu()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.SelectModel(ModelName.Create("缺失").Value);

        Assert.Contains("未生效。", cli.ErrorsOut.Output);
        Assert.Contains("「模型与提供商」", cli.Output.Output);
    }

    [Fact(DisplayName = "取消选择清除当前模型")]
    public void Model_unselect_clears_the_selection()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.UnselectModel();

        Assert.Contains("已保存并生效。", cli.Output.Output);
        Assert.Null(cli.Environment.Models.Current);
    }

    [Fact(DisplayName = "删除当前模型清除选择")]
    public void Model_remove_of_the_current_model_clears_the_selection()
    {
        using ConsoleSession cli = new();

        cli.Catalogs.RemoveModel(ModelName.Create("fake").Value);

        Assert.Contains("已注销当前模型，选择一并取消。", cli.Output.Output);
        Assert.Null(cli.Environment.Models.Current);
    }

    [Fact(DisplayName = "目录导出写入掩码文件")]
    public void Catalog_export_writes_a_masked_file()
    {
        using ConsoleSession cli = new();
        string target = Path.Combine(cli.Environment.Root, "export.json");

        cli.Catalogs.Export(target);

        Assert.True(cli.Environment.Files.FileExists(target), $"{target} 不存在。");
        Assert.Contains("凭据已替换为占位符", cli.Output.Output);
        Assert.DoesNotContain("key", cli.Environment.Files.ReadText(target));
        Assert.Contains("***", cli.Environment.Files.ReadText(target));
    }

    [Fact(DisplayName = "目录导入合并掩码目录")]
    public void Catalog_import_merges_the_masked_catalog()
    {
        using ConsoleSession cli = new();
        string source = Path.Combine(cli.Environment.Root, "export.json");
        cli.Catalogs.Export(source);

        using ConsoleSession fresh = new(seedCatalog: false);
        fresh.Environment.Files.AddFile(source, cli.Environment.Files.ReadText(source));
        fresh.Catalogs.Import(source);

        Assert.Contains("已从", fresh.Output.Output);
        Assert.Equal("test", Assert.Single(fresh.Environment.Catalog.Snapshot().Providers).ProviderName.Value);
    }

    [Fact(DisplayName = "目录菜单显示当前模型并在返回键退出")]
    public void Catalog_menu_shows_the_current_model_and_returns_on_escape()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Catalogs.Browse();

        Assert.Contains("当前模型 fake。", cli.Output.Output);
        Assert.Contains("查看目录", cli.Output.Output);
        Assert.Contains("添加提供商", cli.Output.Output);
    }

    [Fact(DisplayName = "流程菜单列出流程并在返回键退出")]
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

    [Fact(DisplayName = "流程详情渲染节点树")]
    public void Flow_detail_renders_the_node_tree()
    {
        using ConsoleSession cli = new();

        cli.Flows.PrintDetail(new FlowName("默认"));

        Assert.Contains("开始端口", cli.Output.Output);
        Assert.Contains("制定计划", cli.Output.Output);
        Assert.Contains("分配执行", cli.Output.Output);
        Assert.Contains("上游", cli.Output.Output);
        Assert.Contains("├", cli.Output.Output);
        Assert.Contains("└", cli.Output.Output);
    }

    [Fact(DisplayName = "流程详情页刷新后回到流程菜单")]
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

    [Fact(DisplayName = "目录页面保持到返回")]
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

    [Fact(DisplayName = "流程导入报告新增流程并列出")]
    public void Flow_import_reports_new_flows_and_lists_them()
    {
        using ConsoleSession cli = new();
        string file = Path.Combine(cli.Environment.Root, "extra.json");
        cli.Environment.Files.AddFile(file, ImportedFlow);

        cli.Flows.Import(file);

        Assert.Contains("导入 1 条流程。", cli.Output.Output);
        Assert.Contains("新增流程 备用", cli.Output.Output);

        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Flows.Browse();
        Assert.Contains("备用", cli.Output.Output);
    }

    [Fact(DisplayName = "菜单流程导入读取文件路径")]
    public void Flow_import_from_the_menu_reads_the_file_path()
    {
        using ConsoleSession cli = new();
        string file = Path.Combine(cli.Environment.Root, "extra.json");
        cli.Environment.Files.AddFile(file, ImportedFlow);

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

    [Fact(DisplayName = "工具视图列出全部注册工具")]
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

    [Fact(DisplayName = "工具视图渲染配置的命令工具")]
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

    [Fact(DisplayName = "工具视图按路径段分组")]
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

    [Fact(DisplayName = "设置写入并回显生效结果")]
    public void Settings_set_writes_and_echoes_the_effect()
    {
        using ConsoleSession cli = new();

        cli.Settings.Set("Runtime:LogLevel", "Debug");

        Assert.Contains("已保存，重启后生效。", cli.Output.Output);
        Assert.Equal("Debug", cli.Environment.Settings.TryGetUserValue("Runtime:LogLevel"));
    }

    [Fact(DisplayName = "设置语言提示新会话生效")]
    public void Settings_set_language_points_to_a_new_session()
    {
        using ConsoleSession cli = new();

        cli.Settings.Set("Assistant:Language", "English");

        Assert.Contains("内置助手语言已变更，已有会话保持原语言，新建会话使用新语言。", cli.Output.Output);
    }

    [Fact(DisplayName = "设置未知路径被拒绝")]
    public void Settings_set_with_an_unknown_path_is_rejected()
    {
        using ConsoleSession cli = new();

        cli.Settings.Set("Runtime:Nope", "1");

        Assert.Contains("未生效。", cli.ErrorsOut.Output);
    }

    [Fact(DisplayName = "取消设置报告用户层没有该路径")]
    public void Settings_unset_reports_a_path_absent_from_the_user_layer()
    {
        using ConsoleSession cli = new();

        cli.Settings.Unset("Runtime:LogLevel");

        Assert.Contains("Runtime:LogLevel 未在用户层设置。", cli.Output.Output);
    }

    [Fact(DisplayName = "取消设置清除用户层值")]
    public void Settings_unset_clears_a_user_layer_value()
    {
        using ConsoleSession cli = new();
        cli.Settings.Set("Runtime:LogLevel", "Debug");

        cli.Settings.Unset("Runtime:LogLevel");

        Assert.Null(cli.Environment.Settings.TryGetUserValue("Runtime:LogLevel"));
    }

    [Fact(DisplayName = "取消设置未知路径被拒绝")]
    public void Settings_unset_with_an_unknown_path_is_rejected()
    {
        using ConsoleSession cli = new();

        cli.Settings.Unset("Runtime:Nope");

        Assert.Contains("未生效。", cli.ErrorsOut.Output);
    }

    [Fact(DisplayName = "设置清单标记用户层的条目")]
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

    [Fact(DisplayName = "设置菜单列出条目并在返回键退出")]
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

    [Fact(DisplayName = "设置菜单为选中条目写入新值")]
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

    /// <summary>把给出的命令工具文件内容写进内存文件访问并装载，取回配置工具记录。</summary>
    private static IReadOnlyList<ConfiguredTool> Configured(string json)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Kuroe.TestSupport.InMemoryWorkspaceFiles files = new();
        files.AddFile(Path.Combine(directory, "tools.json"), json);

        return new CommandToolFile(files).Load(directory, Path.GetTempPath()).Value;
    }

    /// <summary>导入用的流程文件：与基线库无交叉，可独立装配。</summary>
    private const string ImportedFlow = """
        {
          "Flows": [
            {
              "Name": "备用",
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }],
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                  { "Name": "唯一步骤", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
                ]
              }
            }
          ]
        }
        """;
}