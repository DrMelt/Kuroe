using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Tools;
using Kuroe.Shared.Workflows.Flows;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>流程模型：步骤的缺省配置与按名查步骤。</summary>
public sealed class WorkflowModelTests
{
    [Fact]
    public void StepSpec_defaults_to_single_auto_without_rework()
    {
        StepSpec step = new() { Name = "规划", Role = RunRole.Plan };

        Assert.Equal(StepScope.Single, step.Scope);
        Assert.Equal(StepGate.Auto, step.Gate);
        Assert.Empty(step.From);
        Assert.Null(step.Model);
        Assert.Null(step.OnReject);
    }

    [Fact]
    public void Check_step_defaults_to_retry_once_more()
    {
        StepSpec step = new() { Name = "检查", Role = RunRole.Check };

        Assert.Equal(RejectAction.Retry, step.RejectAction);
        Assert.Equal(2, step.AttemptLimit);
    }

    [Fact]
    public void Declared_rework_overrides_defaults()
    {
        StepSpec step = new()
        {
            Name = "检查",
            Role = RunRole.Check,
            OnReject = RejectAction.Stop,
            MaxAttempts = 5,
        };

        Assert.Equal(RejectAction.Stop, step.RejectAction);
        Assert.Equal(5, step.AttemptLimit);
    }

    [Fact]
    public void Workflow_indexes_steps_by_name()
    {
        Workflow flow = new("默认", null,
        [
            new StepSpec { Name = "规划", Role = RunRole.Plan },
            new StepSpec { Name = "实施", Role = RunRole.Implement },
        ]);

        Assert.Equal(2, flow.Count);
        Assert.Equal(0, flow.IndexOf("规划"));
        Assert.Equal(1, flow.IndexOf("实施"));
        Assert.Null(flow.IndexOf("检查"));
    }

    [Fact]
    public void ToolFunction_exposes_its_declaration()
    {
        ToolFunction function = new("GetTime", "取时间", [], _ => "现在");

        Assert.Equal("GetTime", function.Name);
        Assert.Equal("取时间", function.Description);
        Assert.Empty(function.Parameters);
        Assert.Equal("现在", function.Invoke(new ToolArguments(new Dictionary<string, object?>())));
    }
}