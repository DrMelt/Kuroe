using Kuroe.Agent.Runs;

namespace Kuroe.Workflows.Tasks;

/// <summary>单元在某一刻的只读形状。</summary>
public sealed record UnitSnapshot(
    int? ItemIndex,
    int StepCursor,
    UnitState State,
    UnitVerdict Verdict,
    string? Findings,
    int Attempts,
    IReadOnlyList<RunSnapshot> Runs);
