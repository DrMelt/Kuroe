using ErrorOr;
using Kuroe.Executions;
using Kuroe.Catalogs;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;

namespace Kuroe.Workflows.Tasks;

/// <summary>执行节点到可用模型的解析：取引用的模型配置声明的模型名，未写或目录里没有时报错。</summary>
sealed class NodeModelResolver(CatalogService catalog)
{
    public ErrorOr<string> For(ExecutableNode executable)
    {
        string? model = executable.Model.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            return [RunErrors.ModelNotConfigured(executable.Model.Name.Value)];
        }

        ErrorOr<Success> reachable = catalog.Check(model);

        return reachable.IsError ? reachable.ErrorsOrEmptyList : model;
    }
}