using Kuroe.Flow.Shared.Descriptions;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Tool;

namespace Kuroe.Node.Llm;

/// <summary>Llm 节点的描述契约：字段表与取值到类型实例的转换在此，能力路径与系统指令块的规则在此报出，
/// 未知字段与形状由构造层报出。</summary>
internal sealed class LlmNodeTypeDescription : INodeTypeDescription
{
    private const string PromptName = "Prompt";
    private const string ToolsName = "Tools";
    private const string SystemPromptName = "SystemPrompt";

    /// <summary>形状所属的节点类型名。</summary>
    public NodeKind Kind => LlmNodeTypes.Name;

    /// <summary>Llm 类型的静态形状。</summary>
    public NodeTypeShape Shape => LlmNodeTypes.Shape;

    /// <summary>参数段声明的字段与取值形态。</summary>
    public IReadOnlyList<ParameterField> Fields { get; } =
    [
        new(PromptName, ParameterValueKind.Text),
        new(ToolsName, ParameterValueKind.Texts),
        new(SystemPromptName, ParameterValueKind.Texts),
    ];

    /// <summary>读出参数段并建出类型实例。</summary>
    public INodeType Read(IParameterReader values, Action<string> fail)
    {
        IReadOnlyList<string> tools = values.Texts(ToolsName);
        foreach (string tool in tools)
        {
            if (ToolPath.InvalidReason(tool) is { } reason)
            {
                fail(reason);
            }
        }

        IReadOnlyList<string> systemPrompt = values.Texts(SystemPromptName);
        foreach (string _ in systemPrompt.Where(string.IsNullOrWhiteSpace))
        {
            fail("系统指令块不能为空。");
        }

        return LlmNodeTypes.Create(new LlmParameters
        {
            Prompt = values.Text(PromptName),
            Tools = [.. tools.Select(tool => new ToolPath(tool))],
            SystemPrompt = systemPrompt,
        });
    }

}