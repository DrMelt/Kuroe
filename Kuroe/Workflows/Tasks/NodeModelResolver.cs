using ErrorOr;
using Kuroe.Executions;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Tasks;

/// <summary>执行节点到可用模型的解析：执行节点引用的模型配置未指定时用当前选中的模型，目录里没有时报错。</summary>
sealed class NodeModelResolver(SettingsProvider settings, CatalogService catalog)
{
    public ErrorOr<string> For(ExecutableNode executable)
    {
        string? model = executable.Model.Model ?? settings.Current.Runtime.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            return [RunErrors.ModelNotSelected()];
        }

        ErrorOr<Success> reachable = catalog.Check(model);

        return reachable.IsError ? reachable.ErrorsOrEmptyList : model;
    }
}