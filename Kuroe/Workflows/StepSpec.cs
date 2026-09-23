using Kuroe.Agent;

namespace Kuroe.Workflows;

/// <summary>流程里的一个步骤。步骤是配置，执行体是 agent，角色决定该步骤能交回什么。</summary>
public sealed record StepSpec
{
    public required string Name { get; init; }

    public required RunRole Role { get; init; }

    /// <summary>该步骤使用的模型，未写时用提交任务时选中的模型。</summary>
    public string? Model { get; init; }

    /// <summary>该步骤对模型的额外要求，与目标一起构成指令。</summary>
    public string? Prompt { get; init; }

    public StepScope Scope { get; init; } = StepScope.Single;

    /// <summary>上下文取自哪些前置步骤的产出。</summary>
    public IReadOnlyList<string> From { get; init; } = [];

    public StepGate Gate { get; init; } = StepGate.Auto;

    /// <summary>检查不通过的处置，非检查步骤不得声明。</summary>
    public RejectAction? OnReject { get; init; }

    /// <summary>检查不通过时允许的实施轮数，非检查步骤不得声明。</summary>
    public int? MaxAttempts { get; init; }

    /// <summary>检查步骤未声明时按 Retry 一轮处理。</summary>
    public RejectAction RejectAction => OnReject ?? RejectAction.Retry;

    /// <summary>检查步骤未声明时按两轮处理。</summary>
    public int AttemptLimit => MaxAttempts ?? 2;
}
