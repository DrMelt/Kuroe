using Kuroe.Shared.Executions.Tools;

namespace Kuroe.Tools.Command.Model;

/// <summary>模板里的一个参数项。Text 是命令行文字，可含 {参数名} 占位符；OmitWhenMissing 为关联参数名，
/// 该参数缺省时整个参数项从命令中去掉，null 表示普通参数项。</summary>
public sealed record CommandToolTemplateItem(string Text, string? OmitWhenMissing);

/// <summary>命令工具的配置定义：函数定义、命令模板与执行参数。模板的每一项是一个参数项，里面的 {参数名} 是占位符，运行时由模型实参填充。</summary>
/// <param name="Definition">函数定义：名字、说明、参数与所属分组。</param>
/// <param name="Template">命令行模板，数组每一项是一个参数项。</param>
/// <param name="Directory">命令执行目录，相对工作目录；未给时用工作目录根。</param>
/// <param name="TimeoutSeconds">命令超时秒数。</param>
/// <param name="OutputLimit">交回文本上限。</param>
public sealed record CommandToolDefinition(
    ToolDefinition Definition,
    IReadOnlyList<CommandToolTemplateItem> Template,
    string? Directory,
    int TimeoutSeconds,
    int OutputLimit);
