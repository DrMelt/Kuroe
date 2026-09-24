namespace Kuroe.Shared;

/// <summary>宿主按错误码补充操作指引，这些码由库侧的错误构造与宿主的指引共用。</summary>
public static class ErrorCodes
{
    /// <summary>模型选择不按路径写入，只经 /model 维护。</summary>
    public const string SettingsModelPath = "Settings.ModelPath";

    /// <summary>写入的值是 JSON null。</summary>
    public const string SettingsNullValue = "Settings.NullValue";

    /// <summary>任务号不存在。</summary>
    public const string TaskNotFound = "Task.NotFound";

    /// <summary>agent 号不存在。</summary>
    public const string RunNotFound = "Run.NotFound";

    /// <summary>流程名不存在。</summary>
    public const string WorkflowNotFound = "Workflow.NotFound";

    /// <summary>流程文件不是合法 JSON 或结构不符。</summary>
    public const string WorkflowFormat = "Workflow.Format";

    /// <summary>流程名为空或重复。</summary>
    public const string WorkflowName = "Workflow.Name";

    /// <summary>步骤名不合法。</summary>
    public const string WorkflowStep = "Workflow.Step";

    /// <summary>流程的步骤组合不满足推进依赖的不变量。</summary>
    public const string WorkflowBody = "Workflow.Body";

    /// <summary>命令里的文件参数无法解析为路径。</summary>
    public const string WorkflowInvalidPath = "Workflow.InvalidPath";
}
