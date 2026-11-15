using ErrorOr;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Input.Shared.Tests;

/// <summary>输入交付错误带任务与步骤信息。</summary>
public sealed class InputErrorsTests
{
    [Fact]
    public void Unknown_step_error_names_task_and_step()
    {
        Error error = InputErrors.StepNotAwaiting(
            new TaskId(1),
            FlowStepPath.Of("整体", "收话"));

        Assert.Equal(InputErrors.NotAwaiting, error.Code);
        Assert.Contains("整体/收话", error.Description);
    }
}