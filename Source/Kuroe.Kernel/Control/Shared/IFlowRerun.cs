namespace Kuroe.Control.Shared;

/// <summary>流程控制的可选重跑面。只有支持按步骤重新执行的流程实现才提供。</summary>
public interface IFlowRerun
{
    /// <summary>对阻塞的步骤重新执行，放弃恢复，返回实际发出的重新执行目标数。要求持有任务 Gate。</summary>
    int Rerun();
}