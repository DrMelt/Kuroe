using ErrorOr;
using Kuroe.Agent;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Workflows.Flows;

namespace Kuroe.Workflows.Tasks;

/// <summary>步骤到可用模型的解析：步骤未指定时用当前选中的模型，目录里没有时报错。</summary>
sealed class StepModelResolver(SettingsProvider settings, CatalogService catalog)
{
    public ErrorOr<string> For(StepSpec spec)
    {
        string? model = spec.Model ?? settings.Current.Agent.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            return [AgentErrors.ModelNotSelected()];
        }

        ErrorOr<Success> reachable = catalog.Check(model);

        return reachable.IsError ? reachable.ErrorsOrEmptyList : model;
    }
}