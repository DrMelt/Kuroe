using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Compilation.Projection;

/// <summary>按装配计划把上游产出与流程开始输入取成步骤输入。</summary>
internal sealed class MafStepInputAssembler(IReadOnlyList<MafInputPlan> plans) : IMafStepInputAssembler
{
    /// <inheritdoc/>
    public IReadOnlyList<MafStepInput> Assemble(MafStepInputContext context) =>
        [.. plans.Select(plan => Input(plan, context))];

    private static MafStepInput Input(MafInputPlan plan, MafStepInputContext context)
    {
        MafStepOutput? upstream = Upstream(plan, context);

        return new MafStepInput(
            plan.Port.Name,
            KindOf(plan.Port),
            RoleOf(plan.Port.Role),
            plan.Port.Role == InputRole.Content,
            plan.Port.Default,
            Source(plan),
            SourceKey: null,
            Text(plan, context, upstream),
            plan.UpstreamStep)
        {
            Group = plan.Group,
            Condition = plan.Condition,
            UpstreamPort = plan.UpstreamPort,
            Context = Context(plan, context, upstream),
        };
    }

    /// <summary>取端口文本：装配期常量直接给出，开始输入与上游端口按上下文取。</summary>
    private static string Text(
        MafInputPlan plan,
        MafStepInputContext context,
        MafStepOutput? upstream) => plan.Kind switch
        {
            MafInputKind.Constant => plan.Text ?? string.Empty,
            MafInputKind.StartInput => plan.StartPort is { } port
                ? context.StartInputs.GetValueOrDefault(port) ?? string.Empty
                : string.Empty,
            MafInputKind.Upstream or MafInputKind.RuntimeConstant => upstream?.Text ?? string.Empty,
            _ => string.Empty,
        };

    /// <summary>Context 输入的内容：上游产出携带的内容优先，否则按来源构造单条内容。</summary>
    private static IReadOnlyList<MafContextValue> Context(
        MafInputPlan plan,
        MafStepInputContext context,
        MafStepOutput? upstream)
    {
        if (plan.Port.Type.Semantics != PortSemantics.Context)
        {
            return [];
        }

        if (upstream is { Context.Count: > 0 } output)
        {
            return output.Context;
        }

        string text = plan.Kind switch
        {
            MafInputKind.Constant => plan.Text ?? string.Empty,
            MafInputKind.StartInput => plan.StartPort is { } port
                ? context.StartInputs.GetValueOrDefault(port) ?? string.Empty
                : string.Empty,
            _ => upstream?.Text ?? string.Empty,
        };
        PortName name = plan.Kind switch
        {
            MafInputKind.StartInput => plan.StartPort ?? plan.Port.Name,
            MafInputKind.Upstream or MafInputKind.RuntimeConstant => plan.UpstreamPort ?? plan.Port.Name,
            _ => plan.Port.Name,
        };

        return text.Length == 0 ? [] : [new MafContextValue(name, text, Source(plan))];
    }

    /// <summary>按步骤标识与端口取上游产出，未交回时为空。</summary>
    private static MafStepOutput? Upstream(MafInputPlan plan, MafStepInputContext context)
    {
        if (plan.UpstreamStep is not { } step || plan.UpstreamPort is not { } port)
        {
            return null;
        }

        foreach (MafStepOutput output in context.Upstream)
        {
            if (output.Step == step && output.Port == port)
            {
                return output;
            }
        }

        return null;
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