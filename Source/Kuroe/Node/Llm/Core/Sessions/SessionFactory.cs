using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.Execution;
using Microsoft.Extensions.Logging;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>会话装配入口。公共的推进器与执行器经它取会话，内部实现类型不外露。</summary>
public sealed class SessionFactory(ILoggerFactory loggerFactory, IModelSettings settings, IModelAccess access, ToolCollection tools) : IDisposable
{
    private readonly ClientProvider _clients = new(loggerFactory);
    private readonly IModelSettings _settings = settings;
    private readonly IModelAccess _access = access;
    private readonly ToolCollection _tools = tools;

    /// <summary>一次执行的会话，历史由上游装配的执行输入给出。</summary>
    internal Session ForExecution(ExecutionInput context) =>
        new(_clients, _settings, _access, _tools, new ExecutionHistory(context));

    /// <summary>释放缓存的会话客户端。</summary>
    public void Dispose() => _clients.Dispose();
}
