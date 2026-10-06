using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Executions;

/// <summary>一次 run 的结构化上下文视图：装配内容原样、指令单列，与 <see cref="ContextMessage"/> 同源，
/// 供「ContextOutput」端口整段注入下游时重组，逐条取用，角色与出处保留。run 的系统指令不随帧透传。</summary>
public sealed record ContextFrame(
    RunId Run,
    NodeName Node,
    int? Item,
    IReadOnlyList<ContextMessage> Messages,
    string Instruction);
