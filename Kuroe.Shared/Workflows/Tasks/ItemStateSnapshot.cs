using Kuroe.Shared.Workflows;

namespace Kuroe.Shared.Workflows.Tasks;

/// <summary>拆分里某个条目的流程结论：由哪个检查执行节点交回、检查交回的情况与轮次。</summary>
public sealed record ItemStateSnapshot(
    int ExecutableIndex,
    int ItemIndex,
    string? Branch,
    UnitVerdict Verdict,
    string? Findings,
    int Attempts);