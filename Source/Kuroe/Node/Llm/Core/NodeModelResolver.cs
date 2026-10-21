using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Llm;

/// <summary>执行节点到可用模型的解析：取引用的模型选择声明的模型名，运行时模型取宿主当前选中的模型，
/// 未写或目录里没有时报错。输入节点不启动执行，不进入解析路径。</summary>
public sealed class NodeModelResolver(IModelAccess access, IModelSettings settings) : IExecutionTargetResolver
{
    /// <summary>解析执行节点使用的模型声明。</summary>
    public ErrorOr<INodeDeclaration> For(ExecutableNode executable)
    {
        if (executable.Model is not { } definition)
        {
            return [ModelErrors.ModelMissing(executable.Label.Value)];
        }

        if (definition.Runtime)
        {
            return settings.Model is { } selected
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

        ErrorOr<Success> reachable = access.Check(parsed.Value);

        return reachable.IsError ? reachable.ErrorsOrEmptyList : new KuroeNodeDeclaration(parsed.Value);
    }
}
