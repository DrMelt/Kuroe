using Kuroe.Execution.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Control.Shared;

/// <summary>从存档恢复的流程状态。</summary>
public sealed record FlowRestore(
    IFlowState State,
    FlowTexts StartTexts,
    IReadOnlyList<IExecution> Executions,
    FlowSeed Seed);