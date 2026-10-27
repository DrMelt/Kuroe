using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.Execution;
using Microsoft.Extensions.Logging;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>会话装配入口。公共的推进器与执行器经它取会话，内部实现类型不外露。</summary>
public sealed class SessionFactory(
    ILoggerFactory loggerFactory,
    IModelAccess access,
    IToolFactory tools,
    IToolJson json) : IDisposable
{
    private readonly ClientProvider _clients = new(loggerFactory);
    private readonly IModelAccess _access = access;
    private readonly IToolFactory _tools = tools;
    private readonly IToolJson _json = json;

    /// <summary>一次执行的会话，工具载体与历史都由执行输入给出。</summary>
    internal Session ForExecution(ExecutionId execution, ExecutionInput context) =>
        new(
            _clients,
            _access,
            new ToolCollection(_tools.Create(execution, context), _json),
            new ExecutionHistory(context));

    /// <summary>释放缓存的会话客户端。</summary>
    public void Dispose() => _clients.Dispose();
}
