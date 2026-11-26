using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Maf.Definition;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Maf.Compilation.Projection;

/// <summary>执行步骤的装配计划：输入取用与全部上游步骤。</summary>
internal sealed record MafStepPlan(
    IReadOnlyList<MafInputPlan> Inputs,
    IReadOnlyList<MafStepId> Upstreams);

/// <summary>一个输入绑定的取值计划。</summary>
internal sealed record MafInputPlan(
    InputPort Port,
    MafInputKind Kind,
    string? Text,
    PortName? StartPort,
    MafStepId? UpstreamStep,
    PortName? UpstreamPort,
    int Group,
    MafTriggerCondition Condition)
{
    /// <summary>取值在装配期确定的输入。</summary>
    internal static MafInputPlan Constant(InputPort port, string text) =>
        new(port, MafInputKind.Constant, text, null, null, null, -1, MafTriggerCondition.Positive);

    /// <summary>取值来自流程开始输入的输入。</summary>
    internal static MafInputPlan Start(InputPort port, PortName startPort, int group, MafTriggerCondition condition) =>
        new(port, MafInputKind.StartInput, null, startPort, null, null, group, condition);

    /// <summary>取值来自上游步骤交回端口的输入。</summary>
    internal static MafInputPlan Upstream(
        InputPort port,
        MafStepId step,
        PortName upstreamPort,
        int group,
        MafTriggerCondition condition) =>
        new(port, MafInputKind.Upstream, null, null, step, upstreamPort, group, condition);

    /// <summary>取值来自唯一一次执行产出的运行期常量。</summary>
    internal static MafInputPlan RuntimeConstant(
        InputPort port,
        MafStepId step,
        PortName upstreamPort,
        int group,
        MafTriggerCondition condition) =>
        new(port, MafInputKind.RuntimeConstant, null, null, step, upstreamPort, group, condition);
}

/// <summary>输入取用的来源种类。</summary>
internal enum MafInputKind
{
    /// <summary>取值在装配期确定。</summary>
    Constant,

    /// <summary>取值来自流程开始输入。</summary>
    StartInput,

    /// <summary>取值来自上游步骤交回的端口。</summary>
    Upstream,

    /// <summary>取值来自唯一一次执行产出的运行期常量。</summary>
    RuntimeConstant,
}