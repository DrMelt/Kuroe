using Kuroe.Agent;

namespace Kuroe.Workflows;

/// <summary>步骤在某一刻的只读形状，含该步骤上全部 agent。</summary>
public sealed record StepSnapshot(int Index, StepSpec Spec, IReadOnlyList<RunSnapshot> Runs);