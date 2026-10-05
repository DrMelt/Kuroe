using ErrorOr;
using Kuroe;
using Kuroe.Configuration;
using Kuroe.Executions.Tools;
using Kuroe.Shared;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Tools;
using Kuroe.TestSupport;
using Kuroe.Tools.CommandTools;
using Kuroe.Tools.KuroeTools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Kuroe.Tests;

/// <summary>前台对话的信息查询工具：输出文本供模型回答，值来自各服务的只读快照。</summary>
public sealed class InfoToolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ListTasks_lists_tasks_and_marks_active()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        TaskId id = harness.Submit("写一段百字以内的欢迎致辞");

        TaskInfoTool tool = new(harness.Registry);
        string output = Call(tool, "ListTasks");

        Assert.Contains($"#{id.Value}", output);
        Assert.Contains("写一段百字以内的欢迎致辞", output);
        Assert.Contains("当前对话", output);
    }

    [Fact]
    public void ListTasks_without_tasks_prints_hint()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskInfoTool tool = new(harness.Registry);

        Assert.Contains("还没有任务", Call(tool, "ListTasks"));
        Assert.Contains("还没有任务", Call(tool, "GetActiveTask"));
    }

    [Fact]
    public void GetTask_describes_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        TaskId id = harness.Submit("写一段百字以内的欢迎致辞");

        TaskInfoTool tool = new(harness.Registry);
        string output = Call(tool, "GetTask", ("taskId", id.Value.ToString()));

        Assert.Contains($"#{id.Value}", output);
        Assert.Contains("写一段百字以内的欢迎致辞", output);
        Assert.Contains("目标：", output);
    }

    [Theory]
    [InlineData("x", "任务号要写成数字")]
    [InlineData("99", "没有任务 #99")]
    public void GetTask_rejects_bad_arguments(string taskId, string expected)
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskInfoTool tool = new(harness.Registry);

        Assert.Contains(expected, Call(tool, "GetTask", ("taskId", taskId)));
    }

    [Fact]
    public void GetRun_describes_run_and_result()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        TaskId id = harness.Submit("做一个测试任务");
        harness.Settle(id);

        RunId runId = harness.Snapshot(id).Executables.SelectMany(node => node.Runs).First().Id;
        TaskInfoTool tool = new(harness.Registry);
        string output = Call(tool, "GetRun", ("runId", runId.Value.ToString()));

        Assert.Contains(runId.ToString(), output);
        Assert.Contains("结论：", output);
    }

    [Fact]
    public void GetRun_rejects_bad_arguments()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskInfoTool tool = new(harness.Registry);

        Assert.Contains("run号要写成数字", Call(tool, "GetRun", ("runId", "x")));
        Assert.Contains("没有 run", Call(tool, "GetRun", ("runId", "99")));
    }

    [Fact]
    public void GetCatalog_describes_entries_without_credentials()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        CatalogInfoTool tool = new(harness.Catalog, harness.Models);
        string output = Call(tool, "GetCatalog");

        Assert.Contains("test", output);
        Assert.Contains("fake", output);
        Assert.Contains("凭据已设置", output);
        Assert.DoesNotContain("key", output);
    }
    [Fact]
    public void ListFlows_lists_flows_and_default()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        FlowInfoTool tool = new(harness.Flows);
        string output = Call(tool, "ListFlows");

        Assert.Contains("默认", output);
    }

    [Fact]
    public void GetFlow_describes_nodes()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        FlowInfoTool tool = new(harness.Flows);
        string output = Call(tool, "GetFlow", ("flowName", "默认"));

        Assert.Contains("制定计划（执行节点）", output);
        Assert.Contains("模型 规划者", output);
    }

    [Fact]
    public void GetFlow_reports_missing_flow()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        FlowInfoTool tool = new(harness.Flows);

        Assert.Contains("没有名为 missing 的流程", Call(tool, "GetFlow", ("flowName", "missing")));
    }

    [Fact]
    public void GetConfig_lists_sections()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        SettingsInfoTool tool = new(harness.Settings);
        string output = Call(tool, "GetConfig");

        Assert.Contains("用户层文件：", output);
        Assert.Contains("Runtime", output);
    }
    [Fact]
    public void ListTools_lists_dialogue_functions_and_command_tools()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        ToolboxInfoTool empty = new(harness.CommandTools);
        string noTools = Call(empty, "ListTools");

        Assert.Contains("前台对话可用函数", noTools);
        Assert.Contains("GetLocalTime", noTools);
        Assert.Contains("info", noTools);
        Assert.Contains("files", noTools);
        Assert.Contains("没有配置命令工具", noTools);

        CommandToolDefinition diff = new(
            new ToolName("RunDiff"),
            "查看指定文件相对 HEAD 的改动。",
            [new CommandToolTemplateItem("git", null), new CommandToolTemplateItem("diff", null), new CommandToolTemplateItem("{path}", null)],
            [new ToolParameter(new ToolName("path"), "要查看的文件路径", Required: true)],
            null,
            120,
            8000,
            new ToolPath("git"));

        ToolboxInfoTool tool = new([diff]);
        string output = Call(tool, "ListTools");

        Assert.Contains("git/RunDiff", output);
        Assert.Contains("git diff {path}", output);
        Assert.Contains("path*", output);
    }

    [Fact]
    public void Information_tools_join_the_tool_face()
    {
        using ServiceProvider provider = Services();

        ToolCollection tools = provider.GetRequiredService<ToolCollection>();

        Assert.Contains(new ToolName("ListTasks"), tools.Names);
        Assert.Contains(new ToolName("GetCatalog"), tools.Names);
        Assert.Contains(new ToolName("ListFlows"), tools.Names);
        Assert.Contains(new ToolName("GetConfig"), tools.Names);
        Assert.Contains(new ToolName("ListTools"), tools.Names);
    }

    [Fact]
    public void Command_tool_duplicating_information_tool_name_fails_startup()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, ".kuroe"));
        File.WriteAllText(Path.Combine(_root, ".kuroe", "tools.json"), """
            {
              "Tools": [
                { "Name": "ListTasks", "Template": ["cmd", "/c", "echo", "x"] }
              ]
            }
            """);

        var services = new ServiceCollection();
        services.AddLogging();

        ErrorOr<KuroeStartup> startup = services.AddKuroe(KuroePaths.At(_root));

        Assert.True(startup.IsError);
        Assert.Contains(startup.ErrorsOrEmptyList,
            error => error.Description.Contains("重名", StringComparison.Ordinal));
    }

    /// <summary>在临时工作目录里装配整套服务，用于工具面装配断言。</summary>
    private ServiceProvider Services()
    {
        Directory.CreateDirectory(_root);
        var services = new ServiceCollection();
        services.AddLogging();

        ErrorOr<KuroeStartup> startup = services.AddKuroe(KuroePaths.At(_root));
        Assert.False(startup.IsError);

        return services.BuildServiceProvider();
    }

    private static string Call(ITool tool, string name, params (string Key, object? Value)[] args) =>
        tool.Functions.Single(function => function.Name.Value == name)
            .Invoke(new ToolArguments(args.ToDictionary(pair => pair.Key, pair => pair.Value)));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // 临时目录可能已被清理或占用，收尾不再上报
        }
    }
}
