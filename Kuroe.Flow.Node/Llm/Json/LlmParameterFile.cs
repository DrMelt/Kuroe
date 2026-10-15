using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Node.Llm.Json;

/// <summary>Llm 节点参数段的文件形状：能力路径与系统指令块的规则在此报出，形状与未知字段由文件层报出。</summary>
internal sealed class LlmParameterFile : INodeParameterFile
{
    private const string PromptName = "Prompt";
    private const string ToolsName = "Tools";
    private const string SystemPromptName = "SystemPrompt";

    /// <summary>形状所属的节点类型名。</summary>
    public NodeKind Kind => LlmNodeTypes.Name;

    /// <summary>参数段声明的字段与取值形态。</summary>
    public IReadOnlyList<ParameterField> Fields { get; } =
    [
        new(PromptName, ParameterValueKind.Text),
        new(ToolsName, ParameterValueKind.Texts),
        new(SystemPromptName, ParameterValueKind.Texts),
    ];

    /// <summary>读出参数段。</summary>
    public INodeParameters? Read(IParameterReader values, Action<string> fail)
    {
        IReadOnlyList<string> tools = values.Texts(ToolsName);
        foreach (string tool in tools)
        {
            if (CapabilityPath.InvalidReason(tool) is { } reason)
            {
                fail(reason);
            }
        }

        IReadOnlyList<string> systemPrompt = values.Texts(SystemPromptName);
        foreach (string _ in systemPrompt.Where(string.IsNullOrWhiteSpace))
        {
            fail("系统指令块不能为空。");
        }

        return new LlmParameters
        {
            Prompt = values.Text(PromptName),
            Tools = [.. tools.Select(tool => new CapabilityPath(tool))],
            SystemPrompt = systemPrompt,
        };
    }

    /// <summary>写回参数段：只写有值的字段。</summary>
    public ParameterValues Write(INodeParameters? parameters)
    {
        ParameterValues values = new();
        if (parameters is not LlmParameters llm)
        {
            return values;
        }

        if (llm.Prompt is { Length: > 0 } prompt)
        {
            values.Set(PromptName, prompt);
        }

        if (llm.Tools.Count > 0)
        {
            values.Set(ToolsName, [.. llm.Tools.Select(tool => tool.Value)]);
        }

        if (llm.SystemPrompt.Count > 0)
        {
            values.Set(SystemPromptName, llm.SystemPrompt);
        }

        return values;
    }
}