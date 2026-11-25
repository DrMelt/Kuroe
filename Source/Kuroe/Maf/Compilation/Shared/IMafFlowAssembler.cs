using ErrorOr;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Compilation.Shared;

/// <summary>MAF 流程装配的统一入口：把流程描述编译成 MAF 流程定义。</summary>
public interface IMafFlowAssembler
{
    /// <summary>装配文档中的全部流程，节点库来自文档。</summary>
    ErrorOr<IReadOnlyList<MafFlowDefinition>> Assemble(FlowDescriptionSet document);
}