using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Executions;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;

namespace Kuroe.Workflows.Tasks;

/// <summary>执行节点到可用模型的解析：取引用的模型选择声明的模型名，运行时模型取宿主当前选中的模型，
/// 未写或目录里没有时报错。输入节点不启动 run，不进入解析路径。</summary>
sealed class NodeModelResolver(CatalogService catalog, SettingsProvider settings)
{
    public ErrorOr<ModelName> For(ExecutableNode executable)
    {
        if (executable.Model is not { } definition)
        {
            return [RunErrors.ModelMissing(executable.Name.Value)];
        }

        if (definition.Runtime)
        {
            return settings.Current.Runtime.Model is { } selected ? selected : [RunErrors.ModelNotSelected()];
        }

        ModelName? model = definition.Model;
        if (model is null)
        {
            return [RunErrors.ModelNotConfigured(definition.Name.Value)];
        }

        ErrorOr<Success> reachable = catalog.Check(model);

        return reachable.IsError ? reachable.ErrorsOrEmptyList : model;
    }
}
