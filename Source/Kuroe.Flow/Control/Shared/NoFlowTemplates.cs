using ErrorOr;
using Kuroe.Flow.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Control.Shared;

/// <summary>不提供流程模板的实现。内部流程使用它。</summary>
public sealed class NoFlowTemplates : IFlowTemplateSource
{
    /// <summary>唯一实例。</summary>
    public static NoFlowTemplates Instance { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<FlowTemplateView> All() => [];

    /// <inheritdoc/>
    public ErrorOr<FlowTemplateView> Find(FlowName name) =>
        Error.Failure(FlowErrorCodes.FlowNotFound, $"没有流程模板 {name.Value}。");
}