using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Maf.Definition;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Compilation.Projection;

/// <summary>执行步骤的装配计划：输入取用与唯一上游步骤。</summary>
internal sealed record MafStepPlan(
    IReadOnlyList<MafInputPlan> Inputs,
    MafStepId? Upstream);

/// <summary>一个输入端口的取值计划。</summary>
internal sealed record MafInputPlan(
    InputPort Port,
    MafInputKind Kind,
    string? Text,
    PortName? StartPort,
    MafStepId? UpstreamStep,
    PortName? UpstreamPort)
{
    /// <summary>取值在装配期确定的输入。</summary>
    internal static MafInputPlan Constant(InputPort port, string text) =>
        new(port, MafInputKind.Constant, text, null, null, null);

    /// <summary>取值来自流程开始输入的输入。</summary>
    internal static MafInputPlan Start(InputPort port, PortName startPort) =>
        new(port, MafInputKind.StartInput, null, startPort, null, null);

    /// <summary>取值来自上游步骤交回端口的输入。</summary>
    internal static MafInputPlan Upstream(InputPort port, MafStepId step, PortName upstreamPort) =>
        new(port, MafInputKind.Upstream, null, null, step, upstreamPort);
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
}