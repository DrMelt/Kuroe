using ErrorOr;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Maf.Definition;
using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Maf.Control;

/// <summary>MAF 流程的模板来源：宿主定义的流程进入流程目录，开始端口按内容输入提交。</summary>
public sealed class MafFlowTemplates(IMafFlowSource flows) : IFlowTemplateSource
{
    /// <inheritdoc/>
    public IReadOnlyList<FlowTemplateView> All() => [.. flows.All().Select(Template)];

    /// <inheritdoc/>
    public ErrorOr<FlowTemplateView> Find(FlowName name)
    {
        ErrorOr<MafFlowDefinition> found = flows.Find(name);

        return found.IsError ? found.ErrorsOrEmptyList : Template(found.Value);
    }

    private static FlowTemplateView Template(MafFlowDefinition definition) =>
        new(
            definition.Name.Value,
            definition.Description,
            [.. definition.StartPorts.Select(port => new FlowStartPortView(
                port.Name.Value,
                "Text",
                null,
                port.Default,
                null,
                FlowStartKind.Content,
                null))],
            [.. definition.Steps.Select(step => new FlowStepView(
                step.Id.Value,
                step.Label,
                "Maf",
                FlowItemState.Pending,
                [],
                [.. step.Outputs.Select(port => new FlowPortView(port.Value, "Text", null))],
                [],
                int.MaxValue))],
            []);
}