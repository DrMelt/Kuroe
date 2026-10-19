using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Node.Llm.Sessions;

namespace Kuroe.Node.Llm.Executions;

/// <summary>一次执行的节点侧执行体：会话与过程记录。</summary>
internal sealed class LlmExecution(Session session, ProcessJournal journal) : IExecutionBody
{
    /// <summary>本次执行的会话。</summary>
    public Session Session { get; } = session;

    /// <summary>本次执行的过程记录，节点自己写入。</summary>
    public ProcessJournal Journal { get; } = journal;

    IProcessJournal IExecutionBody.Journal => Journal;
}