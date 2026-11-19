using ErrorOr;
using Kuroe.Flow.Compilation.Documents.Shared;
using Kuroe.Flow.Graph.Shared.StaticGraph.Nodes;
using Kuroe.Maf.Compilation.Shared;
using Kuroe.Maf.Definition;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Compilation;

/// <summary>文档来源的 MAF 流程：装配一次后按流程名提供定义。</summary>
public sealed class MafDocumentFlowSource : IMafFlowSource
{
    private readonly IReadOnlyList<MafFlowDefinition> _flows;
    private readonly Dictionary<FlowName, MafFlowDefinition> _byName;

    private MafDocumentFlowSource(IReadOnlyList<MafFlowDefinition> flows)
    {
        _flows = flows;
        _byName = flows.ToDictionary(flow => flow.Name);
    }

    /// <summary>装配文档并建立流程来源，流程不合法时给出全部错误。</summary>
    public static ErrorOr<MafDocumentFlowSource> Create(
        IMafFlowAssembler assembler,
        FlowDocument document,
        IReadOnlyList<NodeSpec> builtins)
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = assembler.Assemble(document, builtins);
        if (assembled.IsError)
        {
            return assembled.ErrorsOrEmptyList;
        }

        return new MafDocumentFlowSource(assembled.Value);
    }

    /// <inheritdoc/>
    public IReadOnlyList<MafFlowDefinition> All() => _flows;

    /// <inheritdoc/>
    public ErrorOr<MafFlowDefinition> Find(FlowName name) =>
        _byName.TryGetValue(name, out MafFlowDefinition? flow) ? flow : FlowErrors.FlowNotFound(name.Value);
}