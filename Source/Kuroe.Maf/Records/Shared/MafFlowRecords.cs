using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Records.Shared;

/// <summary>MAF 流程的存档定义：实现标识与流程名。</summary>
public sealed record MafFlowDefinitionRecord(
    string Implementation,
    string Flow) : IFlowDefinitionRecord;

/// <summary>MAF 流程的存档状态：开始状态、模型、运行期检查点与未走完的步骤。</summary>
public sealed record MafFlowStateRecord(
    StartInputs Inputs,
    FlowTexts StartTexts,
    string? Model,
    string? CheckpointSession,
    string? CheckpointId,
    string? CheckpointData,
    IReadOnlyList<string> PendingSteps) : IFlowStateRecord;