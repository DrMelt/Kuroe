using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Llm;

/// <summary>执行节点到可用模型的解析：取静态端口 MODEL_NAME 的值，常量直接使用，宿主开始端口取任务启动值，
/// 未声明或目录里没有时报错。输入节点不启动执行，不进入解析路径。</summary>
public sealed class NodeModelResolver(IModelAccess access) : IExecutionTargetResolver
{
    /// <summary>解析执行节点使用的模型值。</summary>
    public ErrorOr<INodeDeclaration> For(ExecutableNode executable, HostStartValues hostValues)
    {
        if (executable.StaticValues is not { } values
            || !values.TryGetValue(LlmNodeTypes.ModelPort, out StaticValue source))
        {
            return [ModelErrors.ModelMissing(executable.Label.Value)];
        }

        if (source.Resolve(hostValues.Text) is not { Length: > 0 } value)
        {
            return [ModelErrors.ModelNotProvided(executable.Label.Value)];
        }

        ErrorOr<ModelName> parsed = ModelName.Create(value);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        ErrorOr<Success> reachable = access.Check(parsed.Value);

        return reachable.IsError ? reachable.ErrorsOrEmptyList : new KuroeNodeDeclaration(parsed.Value);
    }
}
