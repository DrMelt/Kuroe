namespace Kuroe.Flow.Persistence.Code;

/// <summary>代码流程定义的存档形状。</summary>
internal sealed record CodeFlowDefinitionData(
    string? Implementation,
    Dictionary<string, string>? Values);

/// <summary>代码流程状态的存档形状。</summary>
internal sealed record CodeFlowStateData(
    TaskStartData? Start,
    Dictionary<string, string>? Values);

/// <summary>任务开始状态的形状。</summary>
internal sealed record TaskStartData(List<StartInputData>? Inputs, Dictionary<string, string>? StartTexts);

/// <summary>开始输入的形状。</summary>
internal sealed record StartInputData(string? Port, string? Text);