using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Dotnet.Read;

/// <summary>校验源码格式的命令定义。</summary>
internal static class VerifyFormat
{
    /// <summary>参数名：要校验的项目、解决方案或目录。</summary>
    private const string Project = "project";

    /// <summary>参数名：诊断严重级别。</summary>
    private const string Severity = "severity";

    /// <summary>函数定义与模板：dotnet format {project} [--severity {severity}] --verify-no-changes。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("dotnet/read/VerifyFormat"),
        "校验源码是否符合项目里的格式设置，不改动文件；存在待整理的文件时命令以非零退出码结束。project 是项目、解决方案或目录，severity 是诊断严重级别，可选 info、warn、error，省略时用 dotnet 的缺省级别。",
        [
            new ToolParameter(new ToolName(Project), "项目、解决方案或目录路径，缺省在路径基准目录里查找", Path: true),
            new ToolParameter(new ToolName(Severity), "诊断严重级别，可选 info、warn、error，缺省用 dotnet 的缺省级别"),
        ],
        [
            new("dotnet", null),
            new("format", null),
            new("{project}", null),
            new("--severity", Severity),
            new("{severity}", null),
            new("--verify-no-changes", null),
        ],
        TimeoutSeconds: 600,
        OutputLimit: 8000);
}