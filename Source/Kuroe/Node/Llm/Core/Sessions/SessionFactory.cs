using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.Execution;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>会话装配入口。公共的推进器与执行器经它取会话，内部实现类型不外露。</summary>
public sealed class SessionFactory
{
    private readonly ClientProvider _clients;
    private readonly IModelSettings _settings;
    private readonly IModelAccess _access;
    private readonly ToolCollection _tools;

    internal SessionFactory(ClientProvider clients, IModelSettings settings, IModelAccess access, ToolCollection tools)
    {
        _clients = clients;
        _settings = settings;
        _access = access;
        _tools = tools;
    }

    /// <summary>一次执行的会话，历史由上游装配的上下文给出。</summary>
    internal Session ForRun(RunContext context) =>
        new(_clients, _settings, _access, _tools, new RunHistory(context));
}
