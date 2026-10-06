using ErrorOr;
using Kuroe.Executions;
using Kuroe.Catalogs;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;

namespace Kuroe.Workflows.Tasks;

/// <summary>执行节点到可用模型的解析：取引用的模型选择声明的模型名，未写或目录里没有时报错。
/// 输入节点不启动 run，不进入解析路径。</summary>
sealed class NodeModelResolver(CatalogService catalog)
{
    public ErrorOr<string> For(ExecutableNode executable)
    {
        if (executable.Model is not { } definition)
        {
            return [RunErrors.ModelMissing(executable.Name.Value)];
        }

        string? model = definition.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            return [RunErrors.ModelNotConfigured(definition.Name.Value)];
        }

        ErrorOr<Success> reachable = catalog.Check(model);

        return reachable.IsError ? reachable.ErrorsOrEmptyList : model;
    }
}
