using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Maf.Definition;

/// <summary>步骤输入装配的上下文：归属、上游产出、流程开始输入与本次执行使用的模型。</summary>
public sealed record MafStepInputContext(
    ExecutionOwnerId Owner,
    MafStepId Step,
    IReadOnlyList<MafStepOutput> Upstream,
    IReadOnlyDictionary<PortName, string> StartInputs,
    string Model);