using ErrorOr;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Kernel.Input.Shared;

/// <summary>输入交付的错误。</summary>
public static class InputErrors
{
    /// <summary>交付的步骤不在等待输入。</summary>
    public const string NotAwaiting = "Input.NotAwaiting";

    /// <summary>交付的目标步骤没有等待登记。</summary>
    public static Error StepNotAwaiting(TaskId task, FlowStepPath step) =>
        Error.Validation(NotAwaiting, $"任务 {task.Short} 的步骤 {step.Value} 未等待外部输入。");
}