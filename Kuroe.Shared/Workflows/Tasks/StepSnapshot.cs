using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Tasks;

/// <summary>步骤在某一刻的只读形状，含该步骤上全部 agent。</summary>
public sealed record StepSnapshot(int Index, StepSpec Spec, IReadOnlyList<RunSnapshot> Runs);