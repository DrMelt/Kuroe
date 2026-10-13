using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Flows.Shared.Execution;
using Kuroe.Flows.Shared.Execution.Tasks;
using Kuroe.Flows.Execution.Tasks;
using ExecutableNode = Kuroe.Flows.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Executions;

/// <summary>执行节点到可用模型的解析：取引用的模型选择声明的模型名，运行时模型取宿主当前选中的模型，
/// 未写或目录里没有时报错。输入节点不启动 run，不进入解析路径。</summary>
sealed class NodeModelResolver(CatalogService catalog, SettingsProvider settings) : IExecutionTargetResolver
{
    public ErrorOr<INodeDeclaration> For(ExecutableNode executable)
    {
        if (executable.Model is not { } definition)
        {
            return [ModelErrors.ModelMissing(executable.Name.Value)];
        }

        if (definition.Runtime)
        {
            return settings.Current.Runtime.Model is { } selected
                ? new KuroeNodeDeclaration(selected)
                : [ModelErrors.ModelNotSelected()];
        }

        if (definition.Model is not { Length: > 0 } name)
        {
            return [ModelErrors.ModelNotConfigured(definition.Name.Value)];
        }

        ErrorOr<ModelName> parsed = ModelName.Create(name);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        ErrorOr<Success> reachable = catalog.Check(parsed.Value);

        return reachable.IsError ? reachable.ErrorsOrEmptyList : new KuroeNodeDeclaration(parsed.Value);
    }
}
