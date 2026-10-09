using Kuroe.Executions.Tools;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Shared.Executions;

namespace Kuroe.Executions.Sessions;

/// <summary>会话装配入口。公共的推进器与执行器经它取会话，内部实现类型不外露。</summary>
public sealed class SessionFactory
{
    private readonly ClientProvider _clients;
    private readonly SettingsProvider _settings;
    private readonly CatalogService _catalog;
    private readonly ToolCollection _tools;

    internal SessionFactory(ClientProvider clients, SettingsProvider settings, CatalogService catalog, ToolCollection tools)
    {
        _clients = clients;
        _settings = settings;
        _catalog = catalog;
        _tools = tools;
    }

    /// <summary>一次执行的会话，历史由上游装配的上下文给出。</summary>
    internal Session ForRun(RunContext context) =>
        new(_clients, _settings, _catalog, _tools, new RunHistory(context));
}
