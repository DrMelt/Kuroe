using ErrorOr;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Definition;

/// <summary>MAF 流程定义的来源。宿主持有可供提交的 MAF 流程。</summary>
public interface IMafFlowSource
{
    /// <summary>全部 MAF 流程定义。</summary>
    IReadOnlyList<MafFlowDefinition> All();

    /// <summary>按流程名取定义，不存在时返回错误。</summary>
    ErrorOr<MafFlowDefinition> Find(FlowName name);
}

/// <summary>按流程名查找 MAF 流程定义。</summary>
public sealed class MafFlowCatalog(IEnumerable<MafFlowDefinition> flows) : IMafFlowSource
{
    private readonly List<MafFlowDefinition> _flows = [.. flows];

    /// <inheritdoc/>
    public IReadOnlyList<MafFlowDefinition> All() => _flows;

    /// <inheritdoc/>
    public ErrorOr<MafFlowDefinition> Find(FlowName name)
    {
        MafFlowDefinition? found = _flows.FirstOrDefault(flow => flow.Name == name);

        return found is null ? FlowErrors.FlowNotFound(name.Value) : found;
    }
}