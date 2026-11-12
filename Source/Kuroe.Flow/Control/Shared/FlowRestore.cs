using Kuroe.Flow.Shared;
using Kuroe.Flow.Tasks.Records;
using LoopExecution = Kuroe.Flow.Execution.Execution;

namespace Kuroe.Flow.Control;

/// <summary>从存档恢复的流程状态。</summary>
public sealed record FlowRestore(
    IFlowState State,
    FlowTexts StartTexts,
    IReadOnlyList<LoopExecution> Executions,
    FlowSeed Seed);