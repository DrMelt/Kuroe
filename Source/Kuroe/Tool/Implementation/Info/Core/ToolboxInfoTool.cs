using System.Text;
using ErrorOr;
using Kuroe.Presentation;
using Kuroe.Tool.Json;

namespace Kuroe.Tool.Info;

/// <summary>工具面的信息查询工具。列出内置对话可用的函数与配置的命令工具及其路径。</summary>
public sealed class ToolboxInfoTool : ITool
{
    private readonly IReadOnlyList<ToolPath> _dialogueTools;
    private readonly IReadOnlyList<ConfiguredTool> _configuredTools;

    /// <summary>列出工具面的函数定义。</summary>
    private static readonly ToolDefinition ListTools = new(
        new ToolPath("info/ListTools"),
        "列出内置对话可用的函数与配置的命令工具及其路径。",
        []);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用内置对话的能力白名单与配置的工具建立载体。</summary>
    public ToolboxInfoTool(IReadOnlyList<ToolPath> dialogueTools, IReadOnlyList<ConfiguredTool> configuredTools)
    {
        _dialogueTools = dialogueTools;
        _configuredTools = configuredTools;
        Functions = [ListTools.Bind(_ => Describe())];
    }

    /// <summary>工具面的文本概况。</summary>
    private ErrorOr<string> Describe()
    {
        var text = new StringBuilder();
        text.AppendLine("内置对话可用函数与分组：");
        text.AppendLine($"  {string.Join("、", _dialogueTools.OrderBy(path => path.Value))}");

        if (_configuredTools.Count == 0)
        {
            text.Append("没有配置命令工具，写在工作目录 .kuroe/tools.json。");

            return text.ToString();
        }

        text.AppendLine("命令工具：");
        foreach (ConfiguredTool tool in _configuredTools)
        {
            text.AppendLine($"  {tool.Definition.Path.Value} · {tool.Definition.Description}"
                + $" · 模板 {tool.Configuration}"
                + $" · 参数 {tool.Definition.Parameters.Labels()}");
        }

        return text.ToString().TrimEnd();
    }
}
