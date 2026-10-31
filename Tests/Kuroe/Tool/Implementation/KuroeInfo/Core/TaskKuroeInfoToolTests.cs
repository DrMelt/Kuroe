using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Tool.KuroeInfo.Tests;

/// <summary>任务查询工具：空目录提示、未知任务与非法执行号的拒绝。</summary>
public sealed class TaskKuroeInfoToolTests
{
    [Fact]
    public void ListTasks_without_tasks_prints_a_hint()
    {
        TaskKuroeInfoTool tool = new(new FakeTaskReader());

        Assert.Contains("还没有任务", Call(tool, "ListTasks"), StringComparison.Ordinal);
    }

    [Fact]
    public void GetTask_reports_a_missing_task()
    {
        TaskKuroeInfoTool tool = new(new FakeTaskReader());

        Assert.Contains("没有任务 x", Call(tool, "GetTask", ("taskId", "x")), StringComparison.Ordinal);
        Assert.Contains("没有任务 99", Call(tool, "GetTask", ("taskId", "99")), StringComparison.Ordinal);
    }

    [Fact]
    public void GetExecution_reports_bad_arguments()
    {
        TaskKuroeInfoTool tool = new(new FakeTaskReader());

        Assert.Contains("执行号要写成数字", Call(tool, "GetExecution", ("executionId", "x")), StringComparison.Ordinal);
        Assert.Contains("没有执行", Call(tool, "GetExecution", ("executionId", "99")), StringComparison.Ordinal);
    }

    private static string Call(TaskKuroeInfoTool tool, string name, params (string Name, object? Value)[] values) =>
        ToolResult.Render(Function(tool, name).Invoke(new ToolArguments(
            values.ToDictionary(value => value.Name, value => value.Value))));

    private static ToolFunction Function(TaskKuroeInfoTool tool, string name) =>
        tool.Functions.Single(function => function.Definition.Path.Name.Value == name);

    private sealed class FakeTaskReader : ITaskReader
    {
        public ErrorOr<TaskSnapshot> Find(TaskId id) =>
            Error.NotFound("Task.NotFound", $"没有任务 {id.Short}。");

        public ErrorOr<TaskSnapshot> FindByReference(string reference) =>
            Error.NotFound("Task.NotFound", $"没有任务 {reference}。");

        public ErrorOr<ExecutionSnapshot> FindExecution(ExecutionId id) =>
            Error.NotFound("Execution.NotFound", $"没有执行 {id}。");

        public IReadOnlyList<TaskSnapshot> Snapshots() => [];
    }
}
