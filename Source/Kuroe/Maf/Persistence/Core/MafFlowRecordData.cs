namespace Kuroe.Maf.Persistence;

/// <summary>MAF 流程定义的存档形状。</summary>
internal sealed record MafFlowDefinitionData(
    string? Implementation,
    string? Flow);

/// <summary>MAF 流程状态的存档形状。</summary>
internal sealed record MafFlowStateData(
    TaskStartData? Start,
    string? Model,
    string? CheckpointSession,
    string? CheckpointId,
    string? CheckpointData,
    List<string>? PendingSteps);

/// <summary>任务开始状态的形状。</summary>
internal sealed record TaskStartData(List<StartInputData>? Inputs, Dictionary<string, string>? StartTexts);

/// <summary>开始输入的形状。</summary>
internal sealed record StartInputData(string? Port, string? Text);