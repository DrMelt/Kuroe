using ErrorOr;

namespace Kuroe.Tool.Command.Model;

/// <summary>命令工具的配置定义：路径、说明、参数、命令模板与执行参数。模板的每一项是一个参数项，
/// 里面的 {参数名} 是占位符，运行时由模型实参填充；函数定义按这些数据交给工具契约。</summary>
/// <param name="Path">函数在工具层级里的完整路径。</param>
/// <param name="Description">给模型的说明。</param>
/// <param name="Parameters">参数声明。</param>
/// <param name="Template">命令行模板，数组每一项是一个参数项。</param>
/// <param name="Directory">命令执行目录，相对工作目录；未给时用工作目录根。</param>
/// <param name="TimeoutSeconds">命令超时秒数。</param>
/// <param name="OutputLimit">交回文本上限。</param>
public sealed record CommandToolDefinition(
    ToolPath Path,
    string Description,
    IReadOnlyList<CommandToolParameter> Parameters,
    IReadOnlyList<CommandToolTemplateItem> Template,
    string? Directory = null,
    int TimeoutSeconds = CommandToolTemplate.DefaultTimeoutSeconds,
    int OutputLimit = CommandToolTemplate.DefaultOutputLimit)
{
    /// <summary>交给工具契约的函数定义：路径、说明与参数。</summary>
    public ToolDefinition Definition =>
        new(Path, Description, [.. Parameters.Select(ToContract)]);

    /// <summary>按模板与实参展开命令行，第一项是程序本身。返回非空表示实参缺项或模板不合法。</summary>
    public ErrorOr<IReadOnlyList<string>> CommandLine(ToolArguments arguments) =>
        CommandToolRunner.Expand(this, arguments);

    /// <summary>命令侧的参数声明转成工具契约的声明。</summary>
    private static ToolParameter ToContract(CommandToolParameter parameter) =>
        new(new ToolName(parameter.Name), parameter.Description,
            Flag: parameter.Flag, Required: parameter.Required, List: parameter.List);
}
