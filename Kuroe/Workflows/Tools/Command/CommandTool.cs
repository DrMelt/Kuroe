using Kuroe.Shared.Executions.Tools;
using Kuroe.Workflows.Tools.Command.Model;

namespace Kuroe.Workflows.Tools.Command;

/// <summary>命令模板工具载体：按配置定义声明一个可调用的函数，调用体在工作目录执行命令并把输出写回模型。</summary>
public sealed class CommandTool(CommandToolDefinition definition, string baseDirectory) : ITool
{
    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; } =
    [
        definition.Definition.Bind(arguments => CommandToolRunner.Execute(definition, baseDirectory, arguments)),
    ];
}
