namespace Kuroe.Tasks.Shared;

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

    /// <summary>节点库或流程文件不是合法 JSON 或结构不符。</summary>
    public const string FlowFormat = "Flow.Format";

    /// <summary>流程名为空或重复。</summary>
    public const string FlowName = "Flow.Name";

    /// <summary>节点条目不合法：节点名与标签的写位置、形状与取值。</summary>
    public const string FlowNode = "Flow.Node";

    /// <summary>流程的节点组合不满足推进依赖的不变量。</summary>
    public const string FlowBody = "Flow.Body";

    /// <summary>命令里的文件参数无法解析为路径。</summary>
    public const string FlowInvalidPath = "Flow.InvalidPath";

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

    /// <summary>存档版本不受支持。</summary>
    public const string RecordVersion = "Record.Version";

    /// <summary>存档格式不合法。</summary>
    public const string RecordFormat = "Record.Format";

    /// <summary>流程快照无法编译。</summary>
    public const string RecordFlowSnapshot = "Record.FlowSnapshot";

    /// <summary>存档与流程图的节点集合不一致。</summary>
    public const string RecordGraphMismatch = "Record.GraphMismatch";

    /// <summary>存档节点状态与图节点类型不一致。</summary>
    public const string RecordNodeMismatch = "Record.NodeMismatch";

    /// <summary>存档读取失败。</summary>
    public const string RecordRead = "Record.Read";

    /// <summary>任务租约被其他进程持有。</summary>
    public const string TaskLease = "Task.Lease";

    /// <summary>存档写入失败。</summary>
    public const string RecordWrite = "Record.Write";
}