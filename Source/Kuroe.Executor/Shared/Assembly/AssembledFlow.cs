using Kuroe.Tasks.Shared;

namespace Kuroe.Executor.Shared.Assembly;

/// <summary>装配后的流程：流程名、说明、开始端口与节点树。流程名是流程的唯一标识。</summary>
public sealed record AssembledFlow(
    FlowName Name,
    string? Description,
    IReadOnlyList<AssembledStartPort> StartPorts,
    AssembledNode Root);