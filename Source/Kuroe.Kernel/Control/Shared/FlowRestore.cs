using Kuroe.Tasks.Shared;
using Kuroe.Execution.Shared.State;

namespace Kuroe.Control.Shared;

/// <summary>从存档恢复的流程状态。</summary>
public sealed record FlowRestore(
    IFlowState State,
    FlowTexts StartTexts,
    IReadOnlyList<IExecution> Executions,
    FlowSeed Seed);