namespace Kuroe.Flow.Shared;

/// <summary>流程与任务侧的错误码，宿主按码给可操作指引。</summary>
public static class FlowErrorCodes
{
    /// <summary>任务号不存在。</summary>
    public const string TaskNotFound = "Task.NotFound";

    /// <summary>执行号不存在。</summary>
    public const string ExecutionNotFound = "Execution.NotFound";

    /// <summary>任务工作目录已被同名目录或文件占用。</summary>
    public const string TaskWorkspaceConflict = "TaskWorkspace.Conflict";

    /// <summary>任务工作目录无法建立。</summary>
    public const string TaskWorkspaceCreate = "TaskWorkspace.Create";

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
}
