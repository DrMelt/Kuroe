namespace Kuroe.Tool;

/// <summary>工具调用链路的运行时错误码，经渲染为模型文本，不进入宿主错误链路。</summary>
public static class ToolErrorCodes
{
    /// <summary>工具调用参数缺失或取值非法。</summary>
    public const string Argument = "Tool.Argument";

    /// <summary>工具调用的路径解析越界或目标不是可操作的文件。</summary>
    public const string Path = "Tool.Path";

    /// <summary>工具读取工作目录内文件失败。</summary>
    public const string ReadFailed = "Tool.ReadFailed";

    /// <summary>工具写入工作目录内文件失败。</summary>
    public const string WriteFailed = "Tool.WriteFailed";

    /// <summary>命令工具无法启动或超时被终止。</summary>
    public const string Execute = "Tool.Execute";

    /// <summary>工具调用链路上不应出现的内部错误。</summary>
    public const string Internal = "Tool.Internal";
}