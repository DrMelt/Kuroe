using Kuroe.Control.Shared;
using Kuroe.Maf.Records.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Control;

/// <summary>MAF 流程的身份：流程种类、实现标识与流程归属。</summary>
public sealed class MafFlowIdentity(IMafFlowSource flows) : IFlowIdentity
{
    /// <summary>MAF 流程的实现标识。</summary>
    public const string Implementation = "maf";

    /// <inheritdoc/>
    public string DisplayName => "MAF 流程";

    /// <inheritdoc/>
    public bool Handles(FlowName name) => flows.Find(name).IsSuccess;

    /// <inheritdoc/>
    public bool Handles(FlowIdentity identity) =>
        identity.Kind == MafFlowKind.Value && identity.Implementation == Implementation;
}