using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.Code.Records.Shared;

/// <summary>代码定义流程的存档定义：实现种类与流程自有的定义值。</summary>
public sealed record CodeFlowDefinitionRecord(
    string Implementation,
    IReadOnlyDictionary<string, string> Values) : IFlowDefinitionRecord;

/// <summary>代码定义流程的存档状态：开始状态与流程自有的运行状态值。</summary>
public sealed record CodeFlowStateRecord(
    StartInputs Inputs,
    FlowTexts StartTexts,
    IReadOnlyDictionary<string, string> Values) : IFlowStateRecord;