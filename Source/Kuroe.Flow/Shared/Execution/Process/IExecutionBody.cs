namespace Kuroe.Flow.Shared.Execution.Process;

/// <summary>一次执行的节点侧执行体：节点自己收存过程，引擎只从这里按只读契约取记录。</summary>
public interface IExecutionBody
{
    /// <summary>本次执行的过程记录。</summary>
    IProcessJournal Journal { get; }
}