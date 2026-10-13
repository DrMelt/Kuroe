namespace Kuroe.Shared;

/// <summary>错误码由库侧错误构造与进入宿主链路的错误共用，宿主按码给可操作指引。
/// 工具调用链路的 Tool.* 运行时码经渲染为模型文本，不进入宿主错误链路。流程与任务码在引擎侧的 FlowErrorCodes 里。</summary>
public static class ErrorCodes
{
    /// <summary>模型选择不按路径写入，只经 /model 维护。</summary>
    public const string SettingsModelPath = "Settings.ModelPath";

    /// <summary>写入的值是 JSON null。</summary>
    public const string SettingsNullValue = "Settings.NullValue";

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
