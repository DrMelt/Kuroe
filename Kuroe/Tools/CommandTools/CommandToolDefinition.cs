using Kuroe.Shared.Executions.Tools;

namespace Kuroe.Tools.CommandTools;

/// <summary>模板里的一个参数项。Text 是命令行文字，可含 {参数名} 占位符；OmitWhenMissing 为关联参数名，
/// 该参数缺省时整个参数项从命令中去掉，null 表示普通参数项。</summary>
public sealed record CommandToolTemplateItem(string Text, string? OmitWhenMissing);

/// <summary>命令工具的配置定义：一段命令模板与参数声明。模板的每一项是一个参数项，里面的 {参数名} 是占位符，运行时由模型实参填充。</summary>
public sealed record CommandToolDefinition(
    ToolName Name,
    string Description,
    IReadOnlyList<CommandToolTemplateItem> Template,
    IReadOnlyList<ToolParameter> Parameters,
    string? Directory,
    int TimeoutSeconds,
    int OutputLimit,
    ToolPath? Path = null)
{
    /// <summary>完整工具路径：分组加函数名，未给分组时即函数名。白名单按它匹配。</summary>
    public ToolPath FullPath => Path is { } path ? new ToolPath($"{path.Value}/{Name.Value}") : new ToolPath(Name.Value);
}
