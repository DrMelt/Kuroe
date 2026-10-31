using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Tool;
using Kuroe.Tool.KuroeInfo;
using Xunit;

namespace Kuroe.Integration.Tests.Hosting;

/// <summary>内置对话的状态查询工具：输出文本供模型回答，值来自各服务的只读快照。</summary>
public sealed class KuroeInfoToolTests
{
    [Fact]
    public void Task_queries_list_and_describe_the_submitted_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        TaskId id = environment.Submit("写一段百字以内的欢迎致辞");

        TaskKuroeInfoTool tool = new(environment.TaskReader);
        string list = Call(tool, "ListTasks");
        string detail = Call(tool, "GetTask", ("taskId", id.Value.ToString()));

        Assert.Contains($"#{id.Short}", list);
        Assert.Contains("写一段百字以内的欢迎致辞", list);
        Assert.Contains($"#{id.Short}", detail);
        Assert.Contains("写一段百字以内的欢迎致辞", detail);
        Assert.Contains("输入 目标：", detail);
    }

    [Fact]
    public async Task Dialogue_task_is_not_visible_to_task_queries()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = _ => "回复你好";
        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", null);

        TaskKuroeInfoTool tool = new(environment.TaskReader);

        Assert.False(reply.IsError);
        Assert.Contains("还没有任务", Call(tool, "ListTasks"));
        Assert.Contains("没有任务", Call(tool, "GetTask", ("taskId", "1")));
    }

    [Fact]
    public async Task GetExecution_renders_execution_process_and_tool_entry()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Emits = (_, journal) =>
            journal.AppendToolCall("file/ReadFile", "{}", "完成", false);
        TaskId id = environment.Submit("做一个测试任务");
        await environment.SettleAsync(id);

        ExecutionId executionId = environment.Snapshot(id).Executables
            .SelectMany(node => node.Executions)
            .First()
            .Id;
        TaskKuroeInfoTool tool = new(environment.TaskReader);
        string output = Call(tool, "GetExecution", ("executionId", executionId.Value.ToString()))
            .Replace("\r", string.Empty);

        Assert.Contains(executionId.ToString(), output);
        Assert.Contains("过程：", output);
        Assert.Contains("工具 > file/ReadFile {}", output);
        Assert.Contains("\n         结果 完成", output);
    }

    [Fact]
    public void Execution_state_attaches_ongoing_tool_call_progress()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.DelayMs = 60_000;
        environment.Runner.Emits = (_, journal) =>
            journal.AppendToolCall("file/ReadFile", "{}", "完成", false);

        TaskId id = environment.Submit("做一个测试任务");
        TaskSnapshot running = environment.Wait(id, snapshot => snapshot.Executables
            .SelectMany(node => node.Executions)
            .Any(execution => execution.State == ExecutionState.Running
                && execution.Progress.Contains("调用工具 file/ReadFile", StringComparison.Ordinal)));

        ExecutionId executionId = running.Executables
            .SelectMany(node => node.Executions)
            .First(execution => execution.State == ExecutionState.Running)
            .Id;

        TaskKuroeInfoTool tool = new(environment.TaskReader);

        Assert.Contains("执行中（调用工具 file/ReadFile）",
            Call(tool, "GetExecution", ("executionId", executionId.Value.ToString())));
        Assert.Contains("执行中（调用工具 file/ReadFile）",
            Call(tool, "GetTask", ("taskId", id.Value.ToString())));
    }

    [Fact]
    public void ListFlows_lists_flows_and_default()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        FlowKuroeInfoTool tool = new(environment.Flows);
        string output = Call(tool, "ListFlows");

        Assert.Contains("默认", output);
    }

    [Fact]
    public void GetFlow_describes_nodes()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        FlowKuroeInfoTool tool = new(environment.Flows);
        string output = Call(tool, "GetFlow", ("flowName", "默认"));

        Assert.Contains("制定计划（Llm节点）", output);
        Assert.Contains("模型 Start@MODEL_NAME", output);
    }

    [Fact]
    public void GetFlow_shows_host_start_inputs()
    {
        using TestEnvironment environment = TestEnvironment.Create("""
            {
              "Flows": [
                {
                  "Name": "默认",
                  "Start": {
                    "Outputs": [
                      { "Name": "目标", "Type": "Text" },
                      { "Name": "启动路径", "Type": "Text", "Mode": "Constant" },
                      { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }
                    ]
                  },
                  "HostStartInputs": { "启动路径": "WORKSPACE_PATH" },
                  "Nodes": [
                    {
                      "Name": "实施",
                      "Kind": "Llm",
                      "Static": { "MODEL_NAME": "Start@MODEL_NAME", "启动路径": "Start@启动路径" },
                      "Inputs": [ { "Name": "目标" }, { "Name": "启动路径", "Type": "Text", "Mode": "Constant" } ],
                      "From": [ { "目标": "Start@目标" } ]
                    }
                  ]
                }
              ]
            }
            """);

        FlowKuroeInfoTool tool = new(environment.Flows);
        string output = Call(tool, "GetFlow", ("flowName", "默认"));

        Assert.Contains("启动路径←WORKSPACE_PATH", output);
    }

    [Fact]
    public void GetFlow_reports_missing_flow()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        FlowKuroeInfoTool tool = new(environment.Flows);

        Assert.Contains("没有名为 missing 的流程", Call(tool, "GetFlow", ("flowName", "missing")));
    }

    [Fact]
    public void GetFlow_reports_waiting_node_question()
    {
        using TestEnvironment environment = TestEnvironment.Create("""
            {
              "Flows": [
                {
                  "Name": "默认",
                  "Start": { "Outputs": [ { "Name": "目标", "Type": "Text" } ] },
                  "Nodes": [
                    {
                      "Name": "收话",
                      "Kind": "Input",
                      "Question": "请补充背景",
                      "Outputs": [ { "Name": "回答", "Type": "Text" } ],
                      "From": [ { "目标": "Start@目标" } ]
                    }
                  ]
                }
              ]
            }
            """);

        FlowKuroeInfoTool tool = new(environment.Flows);
        string output = Call(tool, "GetFlow", ("flowName", "默认"));

        Assert.Contains("提问 请补充背景", output);
    }


    private static string Call(ITool tool, string name, params (string Key, object? Value)[] args) =>
        ToolResult.Render(tool.Functions.Single(function => function.Definition.Path.Name.Value == name)
            .Invoke(new ToolArguments(args.ToDictionary(pair => pair.Key, pair => pair.Value))));
}
