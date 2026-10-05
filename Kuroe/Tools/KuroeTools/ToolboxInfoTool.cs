using System.Text;
using ErrorOr;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Tools.CommandTools;
using Kuroe.Workflows.Tasks;

namespace Kuroe.Tools.KuroeTools;

/// <summary>工具面的信息查询工具。列出前台对话可用的函数与配置的命令工具及其路径。</summary>
internal sealed class ToolboxInfoTool : ITool
{
    private readonly IReadOnlyList<CommandToolDefinition> _commandTools;

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    public ToolboxInfoTool(IReadOnlyList<CommandToolDefinition> commandTools)
    {
        _commandTools = commandTools;
        Functions =
        [
            new ToolFunction(new ToolName("ListTools"),
                "列出前台对话可用的函数与配置的命令工具及其路径。",
                [], _ => Describe(), new ToolPath("info/ListTools")),
        ];
    }

    /// <summary>工具面的文本概况。</summary>
    private ErrorOr<string> Describe()
    {
        var text = new StringBuilder();
        text.AppendLine("前台对话可用函数与分组：");
        text.AppendLine($"  {string.Join("、", DialogueDefaults.Tools.OrderBy(path => path.Value))}");

        if (_commandTools.Count == 0)
        {
            text.Append("没有配置命令工具，写在工作目录 .kuroe/tools.json。");

            return text.ToString();
        }

        text.AppendLine("命令工具：");
        foreach (CommandToolDefinition tool in _commandTools)
        {
            text.AppendLine($"  {tool.FullPath.Value} · {tool.Description}"
                + $" · 模板 {CommandToolPresentation.Template(tool.Template)}"
                + $" · 参数 {CommandToolPresentation.Parameters(tool.Parameters)}");
        }

        return text.ToString().TrimEnd();
    }
}
