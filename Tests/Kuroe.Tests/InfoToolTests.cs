using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Runtime;
using Kuroe.Shared;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Tool;
using Kuroe.Tool.Command.Json;
using Kuroe.Tool.Info;
using Kuroe.Tool.Json;
using Kuroe.Workflows.Flows;
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
    public void GetExecution_describes_execution_and_process()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        TaskId id = environment.Submit("做一个测试任务");
        environment.Settle(id);

        ExecutionId executionId = environment.Snapshot(id).Executables.SelectMany(node => node.Executions).First().Id;
        TaskInfoTool tool = new(environment.Registry);
        string output = Call(tool, "GetExecution", ("executionId", executionId.Value.ToString()));

        Assert.Contains(executionId.ToString(), output);
        Assert.Contains("过程：", output);
    }

    [Fact]
    public void GetExecution_rejects_bad_arguments()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskInfoTool tool = new(environment.Registry);

        Assert.Contains("执行号要写成数字", Call(tool, "GetExecution", ("executionId", "x")));
        Assert.Contains("没有执行", Call(tool, "GetExecution", ("executionId", "99")));
    }

    [Fact]
    public void Execution_state_attaches_ongoing_tool_call_progress()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.DelayMs = 60_000;

        TaskId id = environment.Submit("做一个测试任务");
        environment.Wait(id, snapshot => snapshot.LiveExecutions > 0);

        ExecutionId executionId = environment.Snapshot(id).Executables
            .SelectMany(node => node.Executions)
            .First(execution => !execution.State.IsSettled())
            .Id;
        Execution execution = environment.Registry.FindExecution(executionId).Value;
        execution.MarkRunning();
        execution.Report("调用工具 file/ReadFile");

        TaskInfoTool tool = new(environment.Registry);

        Assert.Contains("执行中（调用工具 file/ReadFile）",
            Call(tool, "GetExecution", ("executionId", executionId.Value.ToString())));
        Assert.Contains("执行中（调用工具 file/ReadFile）",
            Call(tool, "GetTask", ("taskId", id.Value.ToString())));
    }

    [Fact]
    public void GetCatalog_describes_entries_without_credentials()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        CatalogInfoTool tool = new(new ToolCatalogView(environment.Catalog, environment.Models));
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
        ToolboxInfoTool empty = new(DialogueFlow.Tools, environment.ConfiguredTools);
        string noTools = Call(empty, "ListTools");

        Assert.Contains("内置对话可用函数", noTools);
        Assert.Contains("GetLocalTime", noTools);
        Assert.Contains("info", noTools);
        Assert.Contains("file", noTools);
        Assert.Contains("没有配置命令工具", noTools);

        ConfiguredTool diff = Configured();

        ToolboxInfoTool tool = new(DialogueFlow.Tools, [diff]);
        string output = Call(tool, "ListTools");

        Assert.Contains("git/RunDiff", output);
        Assert.Contains("git diff {path}", output);
        Assert.Contains("path*", output);
    }

    /// <summary>把一份命令工具文件读成配置工具的载体，配置目录为临时新建。</summary>
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
                  "Parameters": [ { "Name": "path", "Description": "要查看的文件路径", "Required": true } ]
                }
              ]
            }
            """);

        return new CommandToolFile().Load(directory, directory).Value[0];
    }

    [Fact]
    public void Command_tool_duplicating_information_tool_path_fails_startup()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, ".kuroe"));
        File.WriteAllText(Path.Combine(_root, ".kuroe", "tools.json"), """
            {
              "Tools": [
                { "Name": "ListTasks", "Group": "info", "Template": ["cmd", "/c", "echo", "x"] }
              ]
            }
            """);

        var services = new ServiceCollection();
        services.AddLogging();

        ErrorOr<KuroeStartup> startup = services.AddKuroe(KuroePaths.At(_root), Path.Combine(_root, "source"));

        Assert.True(startup.IsError);
        Assert.Contains(startup.ErrorsOrEmptyList,
            error => error.Description.Contains("重复", StringComparison.Ordinal));
    }

    private static string Call(ITool tool, string name, params (string Key, object? Value)[] args) =>
        ToolResult.Render(tool.Functions.Single(function => function.Definition.Path.Name.Value == name)
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
