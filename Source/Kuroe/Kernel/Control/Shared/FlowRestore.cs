using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Kernel.Control.Shared;

/// <summary>从存档恢复的流程状态。</summary>
public sealed record FlowRestore(
    IFlowState State,
    FlowTexts StartTexts,
    IReadOnlyList<IExecution> Executions,
    FlowSeed Seed);