using ErrorOr;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.Maf.Compilation.Shared;
using Kuroe.Maf.Definition;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Maf.Compilation;

/// <summary>文档来源的 MAF 流程：逐流程装配，编译成功的流程按名提供，失败的流程进入诊断。</summary>
public sealed class MafDocumentFlowSource : IMafFlowSource
{
    private readonly IReadOnlyList<MafFlowDefinition> _flows;
    private readonly Dictionary<FlowName, MafFlowDefinition> _byName;
    private readonly IReadOnlyList<MafFlowFailure> _rejected;
    private readonly IReadOnlyList<Error> _invalid;

    private MafDocumentFlowSource(
        IReadOnlyList<MafFlowDefinition> flows,
        IReadOnlyList<MafFlowFailure> rejected,
        IReadOnlyList<Error> invalid)
    {
        _flows = flows;
        _byName = [];
        foreach (MafFlowDefinition flow in flows)
        {
            _byName.TryAdd(flow.Name, flow);
        }

        _rejected = rejected;
        _invalid = invalid;
    }

    /// <summary>逐流程装配文档并建立流程来源，失败的流程不进入来源。</summary>
    public static ErrorOr<MafDocumentFlowSource> Create(
        IMafFlowAssembler assembler,
        FlowDescriptionSet document)
    {
        List<MafFlowDefinition> flows = [];
        List<MafFlowFailure> rejected = [];
        List<Error> invalid = [];
        HashSet<FlowName> names = [];
        foreach (FlowEntry entry in document.Flows)
        {
            ErrorOr<FlowName> name = FlowName.Create(entry.Name);
            if (name.IsError)
            {
                invalid.AddRange(name.ErrorsOrEmptyList);
                continue;
            }

            if (!names.Add(name.Value))
            {
                rejected.Add(new MafFlowFailure(name.Value,
                    [FlowErrors.Body(entry.Name, "流程名重复。")]));
                continue;
            }

            ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = assembler.Assemble(
                new FlowDescriptionSet(document.Nodes, [entry]));
            if (assembled.IsError)
            {
                rejected.Add(new MafFlowFailure(name.Value, [.. assembled.ErrorsOrEmptyList]));
                continue;
            }

            flows.AddRange(assembled.Value);
        }

        return new MafDocumentFlowSource(flows, rejected, invalid);
    }

    /// <inheritdoc/>
    public IReadOnlyList<MafFlowDefinition> All() => _flows;

    /// <summary>未进入 MAF 来源的流程与原因。</summary>
    public IReadOnlyList<MafFlowFailure> Rejected => _rejected;

    /// <inheritdoc/>
    public IReadOnlyList<Error> Failures => [.. _invalid, .. _rejected.SelectMany(failure => failure.Errors)];

    /// <inheritdoc/>
    public ErrorOr<MafFlowDefinition> Find(FlowName name) =>
        _byName.TryGetValue(name, out MafFlowDefinition? flow) ? flow : FlowErrors.FlowNotFound(name.Value);
}