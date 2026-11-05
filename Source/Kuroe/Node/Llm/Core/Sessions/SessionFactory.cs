using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Tools;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>会话装配入口。执行器经它取会话，内部实现类型不外露。</summary>
/// <remarks>按模型接入与工具面建立会话装配。</remarks>
public sealed class SessionFactory(IModelAccess access, IToolFactory tools, IToolJson json) : IDisposable
{
    private readonly ClientProvider _clients = new(access);
    private readonly IToolFactory _tools = tools;
    private readonly IToolJson _json = json;

    /// <summary>一次执行的会话，工具载体与历史都由执行输入给出。</summary>
    internal Session ForExecution(ExecutionId execution, ExecutionInput context, ProcessJournal journal) =>
        new(
            _clients,
            new ToolCollection(_tools.Create(execution, context), _json),
            new ExecutionHistory(context),
            journal);

    /// <summary>释放缓存的模型客户端。</summary>
    public void Dispose() => _clients.Dispose();
}
