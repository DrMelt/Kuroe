using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;
using Kuroe.Tool;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>宿主直接调用的任务侧接口：改名、清理与提交校验。</summary>
public sealed class DriverApiTests
{
    [Fact]
    public void Title_and_clear_keep_the_list_in_step()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);
        environment.Tasks.Rename(id, "文档补齐").ThrowIfError();

        Assert.Equal("文档补齐", environment.Snapshot(id).Title);
        Assert.Single(environment.Tasks.ClearSettled());
        Assert.Empty(environment.TaskReader.Snapshots());
    }

    [Fact]
    public void Submission_must_come_from_a_live_execution_of_the_right_role()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README"));
        ExecutionSnapshot implement = Assert.Single(environment.GraphExecutions(done, 1));
        ExecutionId implementExecution = implement.Id;

        ToolFunction submit = Assert.Single(environment.PortToolFor(implementExecution).Functions);
        string receipt = ToolResult.Render(submit.Invoke(
            new ToolArguments(new Dictionary<string, object?> { ["valuesJson"] = """{ "计划": "改一下" }""" })));

        Assert.Contains("没有可交回的端口声明", receipt);
    }

    [Fact]
    public void Unknown_port_name_is_refused_and_leaves_the_step_unclosed()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.PortValuesJson = """{ "别的端口": "值" }""";

        TaskSnapshot blocked = environment.Settle(environment.Submit("补齐 README"));
        ExecutionSnapshot plan = Assert.Single(environment.GraphExecutions(blocked, 0));

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Contains(environment.Runner.Submissions, text => text.Contains("不认识端口 别的端口"));
        Assert.Contains(plan.Failures, failure => failure.Contains("未收口"));
    }
}
