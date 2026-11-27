using ErrorOr;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Maf.Compilation.Shared;

/// <summary>一条未进入 MAF 来源的流程：流程名与装配错误。</summary>
public sealed record MafFlowFailure(FlowName Name, IReadOnlyList<Error> Errors);