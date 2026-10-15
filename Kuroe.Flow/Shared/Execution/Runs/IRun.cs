using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Turns;

namespace Kuroe.Flow.Shared.Execution.Runs;

/// <summary>一次执行对执行器与宿主的视图：所属上下文、回合归属与只读快照。状态改动只在引擎内部。</summary>
public interface IRun
{
    /// <summary>run 标识。</summary>
    RunId Id { get; }

    /// <summary>执行上下文。</summary>
    RunContext Context { get; }

    /// <summary>本次执行的回合归属，工具与提交通道用它认定身份。</summary>
    TurnScope Scope { get; }

    /// <summary>当前执行状态。</summary>
    RunState State { get; }

    /// <summary>当前状态的只读快照。</summary>
    RunSnapshot Snapshot();

    /// <summary>取消本次执行，进行中的一轮请求随之结束。</summary>
    void Cancel();
}
