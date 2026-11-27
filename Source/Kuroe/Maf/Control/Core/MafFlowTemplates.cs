using ErrorOr;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Maf.Definition;
using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Maf.Control;

/// <summary>MAF 流程的模板来源：宿主定义的流程进入流程目录，承载内容的开始端口按内容输入提交。
/// 宿主给出当前模型来源时模型端口按宿主给值呈现，否则按内容输入呈现。</summary>
public sealed class MafFlowTemplates(IMafFlowSource flows, bool hostModel = false) : IFlowTemplateSource
{
    /// <inheritdoc/>
    public IReadOnlyList<FlowTemplateView> All() => [.. flows.All().Select(Template)];

    /// <inheritdoc/>
    public ErrorOr<FlowTemplateView> Find(FlowName name)
    {
        ErrorOr<MafFlowDefinition> found = flows.Find(name);

        return found.IsError ? found.ErrorsOrEmptyList : Template(found.Value);
    }

    private FlowTemplateView Template(MafFlowDefinition definition) =>
        new(
            definition.Name.Value,
            definition.Description,
            [.. definition.StartPorts.Select(port => MafPortView.Template(
                port,
                hostModel && definition.RequiresModel && port.Name == MafFlowDefinition.ModelPort))],
            [.. definition.Steps.Select(step => new FlowStepView(
                step.Id.Value,
                step.Label,
                "Maf",
                FlowItemState.Pending,
                [],
                [.. step.OutputPorts().Select(MafPortView.Output)],
                [],
                int.MaxValue))],
            [])
        {
            Engine = "MAF 流程",
            Implementation = new FlowImplementation(MafFlowIdentity.Implementation),
        };
}