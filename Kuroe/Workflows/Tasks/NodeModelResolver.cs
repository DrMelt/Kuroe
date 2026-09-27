using ErrorOr;
using Kuroe.Agent;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Tasks;

/// <summary>执行节点到可用模型的解析：执行节点引用的 agent 未指定时用当前选中的模型，目录里没有时报错。</summary>
sealed class NodeModelResolver(SettingsProvider settings, CatalogService catalog)
{
    public ErrorOr<string> For(ExecutableNode executable)
    {
        string? model = executable.Agent.Model ?? settings.Current.Agent.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            return [AgentErrors.ModelNotSelected()];
        }

        ErrorOr<Success> reachable = catalog.Check(model);

        return reachable.IsError ? reachable.ErrorsOrEmptyList : model;
    }
}