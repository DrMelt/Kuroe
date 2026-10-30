using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;

namespace Kuroe.Flow.Persistence.Json;

/// <summary>检查点文件的顶层形状。</summary>
internal sealed class TaskCheckpointFileDto
{
    public int Version { get; set; }

    public TaskCheckpointDto? Task { get; set; }
}

/// <summary>任务检查点的文件形状。</summary>
internal sealed class TaskCheckpointDto
{
    public TaskIdentityDto? Identity { get; set; }

    public FlowSnapshotDto? Flow { get; set; }

    public TaskStartDto? Start { get; set; }

    public TaskLifecycle Lifecycle { get; set; }

    public bool Completed { get; set; }

    public TaskJournalDto? Journal { get; set; }

    public List<ExecutionDto>? Executions { get; set; }

    public List<NodeDto>? Nodes { get; set; }

    public SchedulerSeedDto? Pending { get; set; }
}

internal sealed class TaskIdentityDto
{
    public string? Id { get; set; }

    public string? Title { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastActivityAt { get; set; }
}

internal sealed class FlowSnapshotDto
{
    public string? Name { get; set; }

    public string? Document { get; set; }
}

internal sealed class TaskStartDto
{
    public List<StartInputDto>? Inputs { get; set; }

    public Dictionary<string, string>? StartTexts { get; set; }
}

internal sealed class StartInputDto
{
    public string? Port { get; set; }

    public string? Text { get; set; }
}

internal sealed class TaskJournalDto
{
    public List<JournalDto>? Entries { get; set; }

    public int DroppedEntries { get; set; }
}

internal sealed class JournalDto
{
    public DateTimeOffset At { get; set; }

    public JournalLevel Level { get; set; }

    public string? Label { get; set; }

    public string? Text { get; set; }
}

internal sealed class ExecutionDto
{
    public int Id { get; set; }

    public NodePathDto? Node { get; set; }

    public int ExecutionCount { get; set; }

    public ExecutionState State { get; set; }

    public string? Progress { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public List<string>? Failures { get; set; }

    public List<JournalDto>? Journal { get; set; }

    public int DroppedEntries { get; set; }

    public List<InputValueDto>? Inputs { get; set; }

    public Dictionary<string, string>? Texts { get; set; }
}

internal sealed class InputValueDto
{
    public string? Port { get; set; }

    public ContextSourceDto? Origin { get; set; }

    public string? Text { get; set; }
}

internal sealed class ContextSourceDto
{
    public ContextSourceKind Kind { get; set; }

    public int? Execution { get; set; }

    public NodePathDto? Node { get; set; }

    public string? Port { get; set; }
}

internal sealed class NodeDto
{
    public string? Kind { get; set; }

    public NodePathDto? Path { get; set; }

    public NodeCommonDto? Common { get; set; }

    public int? Executions { get; set; }

    public int? PublishedExecution { get; set; }

    public Dictionary<string, PortValueDto>? PublishedPorts { get; set; }

    public string? Content { get; set; }

    public string? Failure { get; set; }

    public bool? FailureTaken { get; set; }

    public bool? Retry { get; set; }

    public string? Answer { get; set; }

    public bool? Waiting { get; set; }

    public NodeInputValuesDto? Inputs { get; set; }

    public bool? Awaiting { get; set; }
}

internal sealed class NodeCommonDto
{
    public long Revision { get; set; }

    public int? LastInputGroup { get; set; }

    public List<ConsumedRevisionDto>? Consumed { get; set; }

    public List<NodePathDto>? ReworkTargets { get; set; }

    public bool Blocked { get; set; }

    public bool RerunRequested { get; set; }
}

internal sealed class ConsumedRevisionDto
{
    public NodePathDto? Source { get; set; }

    public string? Port { get; set; }

    public long Revision { get; set; }
}

internal sealed class NodePathDto
{
    public List<string> Levels { get; set; } = [];
}

internal sealed class PortValueDto
{
    public string? Text { get; set; }

    public bool? Trigger { get; set; }
}

internal sealed class NodeInputValuesDto
{
    public List<NodeInputValueDto>? Ordered { get; set; }

    public List<string>? Connected { get; set; }

    public Dictionary<string, bool>? Triggers { get; set; }

    public Dictionary<string, string>? StaticTexts { get; set; }
}

internal sealed class NodeInputValueDto
{
    public string? Target { get; set; }

    public string? Type { get; set; }

    public InputValueDto? Value { get; set; }
}

internal sealed class SchedulerSeedDto
{
    public List<NodePathDto>? Targets { get; set; }
}
