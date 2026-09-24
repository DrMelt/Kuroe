namespace Kuroe.Shared.Workflows;

/// <summary>工作单元在执行链上的位置状态。</summary>
public enum UnitState
{
    /// <summary>有待执行的步骤或步骤在跑。</summary>
    Working,

    /// <summary>当前步骤产出已就绪，等人批准才开下一步。</summary>
    AwaitingApproval,

    /// <summary>停在某一步，需要人返工或放行才会继续。</summary>
    Blocked,

    /// <summary>全部步骤完成。</summary>
    Done,

    /// <summary>随任务取消而终止。</summary>
    Canceled,
}
