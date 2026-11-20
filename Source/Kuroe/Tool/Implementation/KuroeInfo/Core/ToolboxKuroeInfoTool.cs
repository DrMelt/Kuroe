using System.Text;
using ErrorOr;
using Kuroe.Tool.Json;

namespace Kuroe.Tool.Implementation.KuroeInfo;

/// <summary>内置对话的一个模式及其放行的工具路径前缀。</summary>
public sealed record DialogueToolGroup(string Name, IReadOnlyList<ToolPath> Tools);

/// <summary>工具面的状态查询工具。列出内置对话各模式放行的路径前缀与配置的命令工具及其调用名与路径。</summary>
public sealed class ToolboxKuroeInfoTool : ITool
{
    private readonly IReadOnlyList<DialogueToolGroup> _dialogueGroups;
    private readonly IReadOnlyList<ConfiguredTool> _configuredTools;

    /// <summary>列出工具面的函数定义。</summary>
    private static readonly ToolDefinition ListTools = new(
        new ToolPath("KuroeInfo/ListTools"),
        "列出内置对话各模式放行的路径前缀与配置的命令工具及其调用名与路径。",
        []);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用内置对话各模式的工具白名单与配置的工具建立载体。</summary>
    public ToolboxKuroeInfoTool(
        IReadOnlyList<DialogueToolGroup> dialogueGroups,
        IReadOnlyList<ConfiguredTool> configuredTools)
    {
        _dialogueGroups = dialogueGroups;
        _configuredTools = configuredTools;
        Functions = [ListTools.Bind(_ => Describe())];
    }

    /// <summary>工具面的文本概况。</summary>
    private ErrorOr<string> Describe()
    {
        var text = new StringBuilder();
        text.AppendLine("内置对话各模式放行的路径前缀：");
        foreach (DialogueToolGroup group in _dialogueGroups)
        {
            text.AppendLine($"  {group.Name}：{string.Join("、", group.Tools.OrderBy(path => path.Value))}");
        }

        text.AppendLine("调用名是路径的分隔符换成下划线，调用按调用名写。实际可调用的函数以本轮工具面为准。");

        if (_configuredTools.Count == 0)
        {
            text.Append("没有配置命令工具，写在工作目录 .kuroe/tools.json。");

            return text.ToString();
        }

        text.AppendLine("命令工具：");
        foreach (ConfiguredTool tool in _configuredTools)
        {
            text.AppendLine($"  {tool.Definition.Path.CallName} · 路径 {tool.Definition.Path.Value} · {tool.Definition.Description}"
                + $" · 模板 {tool.Configuration}"
                + $" · 参数 {tool.Definition.Parameters.Labels()}");
        }

        return text.ToString().TrimEnd();
    }
}
