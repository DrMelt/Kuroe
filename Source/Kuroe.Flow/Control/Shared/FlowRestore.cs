using Kuroe.Flow.Execution;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Control;

/// <summary>从存档恢复的流程状态。</summary>
public sealed record FlowRestore(
    IFlowState State,
    FlowTexts StartTexts,
    IReadOnlyList<IExecution> Executions,
    FlowSeed Seed);