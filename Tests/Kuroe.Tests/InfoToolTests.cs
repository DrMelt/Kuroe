using ErrorOr;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Execution.Runs;
using Kuroe.Flow.Node.Llm.Tools;
using Kuroe.Shared;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Tool;
using Kuroe.Workflows.Tools.Command.Model;
using Kuroe.Workflows.Tools.Info;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kuroe.Tests;

/// <summary>内置对话的信息查询工具：输出文本供模型回答，值来自各服务的只读快照。</summary>
public sealed class InfoToolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ListTasks_lists_tasks()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        TaskId id = environment.Submit("写一段百字以内的欢迎致辞");

        TaskInfoTool tool = new(environment.Registry);
        string output = Call(tool, "ListTasks");

        Assert.Contains($"#{id.Value}", output);
        Assert.Contains("写一段百字以内的欢迎致辞", output);
    }

    [Fact]
    public void ListTasks_without_tasks_prints_hint()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskInfoTool tool = new(environment.Registry);

        Assert.Contains("还没有任务", Call(tool, "ListTasks"));
    }

    [Fact]
    public void GetTask_describes_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        TaskId id = environment.Submit("写一段百字以内的欢迎致辞");

        TaskInfoTool tool = new(environment.Registry);
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
        using TestEnvironment environment = TestEnvironment.Create();

        TaskInfoTool tool = new(environment.Registry);

        Assert.Contains(expected, Call(tool, "GetTask", ("taskId", taskId)));
    }

    [Fact]
    public void GetRun_describes_run_and_result()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        TaskId id = environment.Submit("做一个测试任务");
        environment.Settle(id);

        RunId runId = environment.Snapshot(id).Executables.SelectMany(node => node.Runs).First().Id;
        TaskInfoTool tool = new(environment.Registry);
        string output = Call(tool, "GetRun", ("runId", runId.Value.ToString()));

        Assert.Contains(runId.ToString(), output);
        Assert.Contains("结论：", output);
    }

    [Fact]
    public void GetRun_rejects_bad_arguments()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskInfoTool tool = new(environment.Registry);

        Assert.Contains("run号要写成数字", Call(tool, "GetRun", ("runId", "x")));
        Assert.Contains("没有 run", Call(tool, "GetRun", ("runId", "99")));
    }

    [Fact]
    public void Run_state_attaches_ongoing_tool_call_progress()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Executor.DelayMs = 60_000;

        TaskId id = environment.Submit("做一个测试任务");
        environment.Wait(id, snapshot => snapshot.LiveRuns > 0);

        RunId runId = environment.Snapshot(id).Executables
            .SelectMany(node => node.Runs)
            .First(run => !run.IsSettled)
            .Id;
        Run run = environment.Registry.FindRun(runId).Value;
        run.MarkRunning();
        run.Report("调用工具 files/ReadFile");

        TaskInfoTool tool = new(environment.Registry);

        Assert.Contains("执行中（调用工具 files/ReadFile）",
            Call(tool, "GetRun", ("runId", runId.Value.ToString())));
        Assert.Contains("执行中（调用工具 files/ReadFile）",
            Call(tool, "GetTask", ("taskId", id.Value.ToString())));
    }

    [Fact]
    public void GetCatalog_describes_entries_without_credentials()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        CatalogInfoTool tool = new(environment.Catalog, environment.Models);
        string output = Call(tool, "GetCatalog");

        Assert.Contains("test", output);
        Assert.Contains("fake", output);
        Assert.Contains("凭据已设置", output);
        Assert.DoesNotContain("key", output);
    }
    [Fact]
    public void ListFlows_lists_flows_and_default()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        FlowInfoTool tool = new(environment.Flows);
        string output = Call(tool, "ListFlows");

        Assert.Contains("默认", output);
    }

    [Fact]
    public void GetFlow_describes_nodes()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        FlowInfoTool tool = new(environment.Flows);
        string output = Call(tool, "GetFlow", ("flowName", "默认"));

        Assert.Contains("制定计划（Llm节点）", output);
        Assert.Contains("模型 规划者", output);
    }

    [Fact]
    public void GetFlow_reports_missing_flow()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        FlowInfoTool tool = new(environment.Flows);

        Assert.Contains("没有名为 missing 的流程", Call(tool, "GetFlow", ("flowName", "missing")));
    }

    [Fact]
    public void GetConfig_lists_sections()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        SettingsInfoTool tool = new(environment.Settings);
        string output = Call(tool, "GetConfig");

        Assert.Contains("用户层文件：", output);
        Assert.Contains("Runtime", output);
    }
    [Fact]
    public void ListTools_lists_dialogue_functions_and_command_tools()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        ToolboxInfoTool empty = new(environment.CommandTools);
        string noTools = Call(empty, "ListTools");

        Assert.Contains("内置对话可用函数", noTools);
        Assert.Contains("GetLocalTime", noTools);
        Assert.Contains("info", noTools);
        Assert.Contains("files", noTools);
        Assert.Contains("没有配置命令工具", noTools);

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

        ErrorOr<KuroeStartup> startup = services.AddKuroe(KuroePaths.At(_root), Path.Combine(_root, "source"));

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

        ErrorOr<KuroeStartup> startup = services.AddKuroe(KuroePaths.At(_root), Path.Combine(_root, "source"));
        Assert.False(startup.IsError);

        return services.BuildServiceProvider();
    }

    private static string Call(ITool tool, string name, params (string Key, object? Value)[] args) =>
        ToolResult.Render(tool.Functions.Single(function => function.Definition.Name.Value == name)
            .Invoke(new ToolArguments(args.ToDictionary(pair => pair.Key, pair => pair.Value))));

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
