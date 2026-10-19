namespace Kuroe.Shared;

/// <summary>错误码由库侧错误构造与进入宿主链路的错误共用，宿主按码给可操作指引。
/// 工具调用链路的运行时码在工具契约的 ToolErrorCodes 里；引擎侧的契约提交链不依赖工具契约，以同值文本产出 Tool.Argument 与 Tool.Internal。
/// 流程与任务码在引擎侧的 FlowErrorCodes 里。</summary>
public static class ErrorCodes
{
    /// <summary>模型选择不按路径写入，只经 /model 维护。</summary>
    public const string SettingsModelPath = "Settings.ModelPath";

    /// <summary>写入的值是 JSON null。</summary>
    public const string SettingsNullValue = "Settings.NullValue";

    /// <summary>工具文件无法读取。</summary>
    public const string ToolRead = "Tool.Read";

    /// <summary>工具文件不是合法 JSON 或结构不符。</summary>
    public const string ToolFormat = "Tool.Format";

    /// <summary>命令工具定义本身不合规，如占位符与参数声明不匹配。</summary>
    public const string ToolInvalid = "Tool.Invalid";

    /// <summary>配置的工具与其他已注册工具重名。</summary>
    public const string ToolDuplicate = "Tool.Duplicate";
}
