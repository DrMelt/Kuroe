using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Compilation.Projection;

/// <summary>按装配计划把上游产出与流程开始输入取成步骤输入。</summary>
internal sealed class MafStepInputAssembler(IReadOnlyList<MafInputPlan> plans) : IMafStepInputAssembler
{
    /// <inheritdoc/>
    public IReadOnlyList<MafStepInput> Assemble(MafStepInputContext context) =>
        [.. plans.Select(plan => Input(plan, context))];

    private static MafStepInput Input(MafInputPlan plan, MafStepInputContext context) =>
        new(
            plan.Port.Name,
            KindOf(plan.Port),
            RoleOf(plan.Port.Role),
            plan.Port.Role == InputRole.Content,
            plan.Port.Default,
            Source(plan),
            SourceKey: null,
            Text(plan, context),
            plan.UpstreamStep)
        {
            Group = plan.Group,
            Condition = plan.Condition,
            UpstreamPort = plan.UpstreamPort,
        };

    /// <summary>取端口文本：装配期常量直接给出，开始输入与上游端口按上下文取。</summary>
    private static string Text(MafInputPlan plan, MafStepInputContext context) => plan.Kind switch
    {
        MafInputKind.Constant => plan.Text ?? string.Empty,
        MafInputKind.StartInput => plan.StartPort is { } port
            ? context.StartInputs.GetValueOrDefault(port) ?? string.Empty
            : string.Empty,
        MafInputKind.Upstream or MafInputKind.RuntimeConstant => Upstream(plan, context),
        _ => string.Empty,
    };

    /// <summary>按步骤标识与端口取上游交回的文本，未交回时为空。</summary>
    private static string Upstream(MafInputPlan plan, MafStepInputContext context)
    {
        if (plan.UpstreamStep is not { } step || plan.UpstreamPort is not { } port)
        {
            return string.Empty;
        }

        foreach (MafStepOutput output in context.Upstream)
        {
            if (output.Step == step && output.Port == port)
            {
                return output.Text;
            }
        }

        return string.Empty;
    }

    /// <summary>输入取用的来源描述。</summary>
    private static string Source(MafInputPlan plan) => plan.Kind switch
    {
        MafInputKind.StartInput => plan.StartPort is { } port ? $"流程开始端口「{port}」" : string.Empty,
        MafInputKind.Upstream => plan.UpstreamStep is { } step && plan.UpstreamPort is { } port
            ? $"步骤「{step}」的端口「{port}」产出"
            : string.Empty,
        MafInputKind.RuntimeConstant => plan.UpstreamStep is { } step && plan.UpstreamPort is { } port
            ? $"步骤「{step}」的运行期常量「{port}」"
            : string.Empty,
        _ => $"输入端口「{plan.Port.Name}」的固定值",
    };

    private static ExecutionPortKind KindOf(InputPort port) => port.Type.Semantics switch
    {
        PortSemantics.Context => ExecutionPortKind.Context,
        PortSemantics.Signal => ExecutionPortKind.Signal,
        _ => ExecutionPortKind.Text,
    };

    private static ExecutionPortRole RoleOf(InputRole role) => role switch
    {
        InputRole.Configuration => ExecutionPortRole.Configuration,
        InputRole.Control => ExecutionPortRole.Control,
        _ => ExecutionPortRole.Content,
    };
}