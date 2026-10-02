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

    /// <summary>run 号不存在。</summary>
    public const string RunNotFound = "Run.NotFound";

    /// <summary>流程名不存在。</summary>
    public const string FlowNotFound = "Flow.NotFound";

    /// <summary>流程文件不是合法 JSON 或结构不符。</summary>
    public const string FlowFormat = "Flow.Format";

    /// <summary>流程名为空或重复。</summary>
    public const string FlowName = "Flow.Name";

    /// <summary>节点名不合法。</summary>
    public const string FlowNode = "Flow.Node";

    /// <summary>流程的节点组合不满足推进依赖的不变量。</summary>
    public const string FlowBody = "Flow.Body";

    /// <summary>命令里的文件参数无法解析为路径。</summary>
    public const string FlowInvalidPath = "Flow.InvalidPath";
}
