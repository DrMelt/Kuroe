using Kuroe.Control.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Control;

/// <summary>运行期检查点：会话标识、检查点标识与 JSON 文本，三者同时有效。</summary>
internal sealed record MafCheckpoint(string Session, string Id, string Data);

/// <summary>MAF 流程状态：开始输入、模型、运行期检查点与未走完的步骤。</summary>
internal sealed class MafFlowState(
    MafFlowDefinition definition,
    StartInputs inputs,
    FlowTexts startTexts,
    string model) : IFlowState
{
    /// <inheritdoc/>
    public StartInputs Inputs { get; } = inputs;

    /// <summary>流程定义。</summary>
    internal MafFlowDefinition Definition { get; } = definition;

    /// <summary>开始端口文本。</summary>
    internal FlowTexts StartTexts { get; } = startTexts;

    /// <summary>执行使用的模型名。</summary>
    internal string Model { get; } = model;

    /// <summary>运行期检查点。</summary>
    internal MafCheckpoint? Checkpoint { get; set; }

    /// <summary>检查点写入是否冻结。中断后恢复必须从步骤前的检查点开始。</summary>
    internal bool FreezeCheckpoint { get; set; }

    /// <summary>尚未走完的步骤标识。</summary>
    internal List<string> PendingSteps { get; } = [];
}

/// <summary>MAF 流程状态的读取。</summary>
internal static class MafFlowAccess
{
    internal static MafFlowState State(ITaskFlowContext task) =>
        task.FlowState as MafFlowState
        ?? throw new InvalidOperationException("任务不是 MAF 流程。");
}