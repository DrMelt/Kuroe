using ErrorOr;
using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe.Flow.Compiler.Shared;

/// <summary>流程描述的统一编译入口：文档构造、库展开、模型校验与图编译。</summary>
public interface IFlowDescriptionCompiler
{
    /// <summary>编译一份描述文档。内置定义由调用方给出，文件定义来自文档。</summary>
    ErrorOr<CompiledDocument> Compile(FlowDocument document, IReadOnlyList<NodeSpec> builtins);
}