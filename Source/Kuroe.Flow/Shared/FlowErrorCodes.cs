namespace Kuroe.Flow.Shared;

/// <summary>流程与任务侧的错误码，宿主按码给可操作指引。</summary>
public static class FlowErrorCodes
{
    /// <summary>任务号不存在。</summary>
    public const string TaskNotFound = "Task.NotFound";

    /// <summary>执行号不存在。</summary>
    public const string ExecutionNotFound = "Execution.NotFound";

    /// <summary>提交的开始输入与开始端口不一致。</summary>
    public const string TaskInput = "Task.Input";

    /// <summary>流程名不存在。</summary>
    public const string FlowNotFound = "Flow.NotFound";

    /// <summary>流程文件不是合法 JSON 或结构不符。</summary>
    public const string FlowFormat = "Flow.Format";

    /// <summary>流程名为空或重复。</summary>
    public const string FlowName = "Flow.Name";

    /// <summary>节点条目不合法：节点名与标签的写位置、形状与取值。</summary>
    public const string FlowNode = "Flow.Node";

    /// <summary>流程的节点组合不满足推进依赖的不变量。</summary>
    public const string FlowBody = "Flow.Body";

    /// <summary>命令里的文件参数无法解析为路径。</summary>
    public const string FlowInvalidPath = "Flow.InvalidPath";

    /// <summary>导入的文件按自身装配失败。</summary>
    public const string FlowImport = "Flow.Import";

    /// <summary>任务已暂停，不能交付外部输入或发起推进动作。</summary>
    public const string TaskPaused = "Task.Paused";

    /// <summary>任务未暂停。</summary>
    public const string TaskNotPaused = "Task.NotPaused";

    /// <summary>任务已暂停。</summary>
    public const string TaskAlreadyPaused = "Task.AlreadyPaused";

    /// <summary>任务已完成，不能暂停。</summary>
    public const string TaskDoneCannotPause = "Task.DoneCannotPause";

    /// <summary>任务已完成，不能终止。</summary>
    public const string TaskDoneCannotTerminate = "Task.DoneCannotTerminate";

    /// <summary>任务已终止。</summary>
    public const string TaskTerminated = "Task.Terminated";

    /// <summary>任务不可恢复。</summary>
    public const string TaskNotResumable = "Task.NotResumable";

    /// <summary>检查点版本不受支持。</summary>
    public const string CheckpointVersion = "Checkpoint.Version";

    /// <summary>检查点格式不合法。</summary>
    public const string CheckpointFormat = "Checkpoint.Format";

    /// <summary>流程快照无法编译。</summary>
    public const string CheckpointFlowSnapshot = "Checkpoint.FlowSnapshot";

    /// <summary>检查点与流程图的节点集合不一致。</summary>
    public const string CheckpointGraphMismatch = "Checkpoint.GraphMismatch";

    /// <summary>检查点节点状态与图节点类型不一致。</summary>
    public const string CheckpointNodeMismatch = "Checkpoint.NodeMismatch";

    /// <summary>检查点读取失败。</summary>
    public const string CheckpointRead = "Checkpoint.Read";

    /// <summary>任务租约被其他进程持有。</summary>
    public const string TaskLease = "Task.Lease";

    /// <summary>检查点写入失败。</summary>
    public const string CheckpointWrite = "Checkpoint.Write";
}
