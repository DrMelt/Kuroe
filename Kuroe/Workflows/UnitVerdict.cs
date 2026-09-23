namespace Kuroe.Workflows;

/// <summary>工作单元的流程结论，由检查步骤交回，与 agent 的执行状态分轴。</summary>
public enum UnitVerdict
{
    /// <summary>还没有检查步骤交回结论。</summary>
    NotChecked,

    /// <summary>检查通过。</summary>
    Verified,

    /// <summary>检查不通过，按检查步骤的处置决定返工或停止。</summary>
    Rejected,
}
