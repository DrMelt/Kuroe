using ErrorOr;
using Kuroe.Flow.Compilation.Documents.Shared;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Compilation.Shared;

/// <summary>MAF 流程装配的统一入口：把描述文档编译成 MAF 流程定义。</summary>
public interface IMafFlowAssembler
{
    /// <summary>装配文档中的全部流程。内置定义由调用方给出，文件定义来自文档。</summary>
    ErrorOr<IReadOnlyList<MafFlowDefinition>> Assemble(
        FlowDocument document,
        IReadOnlyList<NodeSpec> builtins);
}