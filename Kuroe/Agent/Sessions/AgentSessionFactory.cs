using Kuroe.Agent.Tools;
using Kuroe.Catalogs;
using Kuroe.Configuration;

namespace Kuroe.Agent.Sessions;

/// <summary>会话装配入口。公共的推进器与执行器经它取会话，内部实现类型不外露。</summary>
public sealed class AgentSessionFactory
{
    private readonly AgentClientProvider _clients;
    private readonly SettingsProvider _settings;
    private readonly CatalogService _catalog;
    private readonly ToolCollection _tools;

    internal AgentSessionFactory(AgentClientProvider clients, SettingsProvider settings, CatalogService catalog, ToolCollection tools)
    {
        _clients = clients;
        _settings = settings;
        _catalog = catalog;
        _tools = tools;
    }

    /// <summary>任务的前台会话，历史按当前设置装配。</summary>
    internal AgentSession NewDialogue() =>
        new(_clients, _settings, _catalog, _tools, new DialogueHistory(_settings));

    /// <summary>agent 的会话，历史由上游装配的上下文给出。</summary>
    internal AgentSession ForRun(RunContext context) =>
        new(_clients, _settings, _catalog, _tools, new RunHistory(_settings, context));
}
