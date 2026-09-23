namespace Kuroe.Workflows.Flows;

/// <summary>步骤产出后是否等人放行。</summary>
public enum StepGate
{
    /// <summary>产出即开下一步。</summary>
    Auto,

    /// <summary>停在等人批准。</summary>
    Review,
}
