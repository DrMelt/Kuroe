namespace Kuroe.Shared;

/// <summary>错误码由库侧错误构造与进入宿主链路的错误共用，宿主按码给可操作指引。
/// 工具调用链路的 Tool.* 运行时码经渲染为模型文本，不进入宿主错误链路。</summary>
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

    /// <summary>导入的文件按自身装配失败。</summary>
    public const string FlowImport = "Flow.Import";

    /// <summary>命令工具文件无法读取。</summary>
    public const string ToolRead = "Tool.Read";

    /// <summary>命令工具文件不是合法 JSON 或结构不符。</summary>
    public const string ToolFormat = "Tool.Format";

    /// <summary>命令工具定义本身不合规，如占位符与参数声明不匹配。</summary>
    public const string ToolInvalid = "Tool.Invalid";

    /// <summary>命令工具与其他已注册工具重名。</summary>
    public const string ToolDuplicate = "Tool.Duplicate";

    /// <summary>工具调用参数缺失或取值非法。</summary>
    public const string ToolArgument = "Tool.Argument";

    /// <summary>工具调用的路径解析越界或目标不是可操作的文件。</summary>
    public const string ToolPath = "Tool.Path";

    /// <summary>工具读取工作目录内文件失败。</summary>
    public const string ToolReadFailed = "Tool.ReadFailed";

    /// <summary>工具写入工作目录内文件失败。</summary>
    public const string ToolWriteFailed = "Tool.WriteFailed";

    /// <summary>命令工具无法启动或超时被终止。</summary>
    public const string ToolExecute = "Tool.Execute";

    /// <summary>工具调用链路上不应出现的内部错误。</summary>
    public const string ToolInternal = "Tool.Internal";
}
