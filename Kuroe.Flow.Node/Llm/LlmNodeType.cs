using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Node.Llm;

/// <summary>Llm 类型：由模型执行并交回端口产出。运行期对象由引擎按形状建立。</summary>
internal sealed class LlmNodeType : INodeType, INodeParameterFile
{
    private const string PromptName = "Prompt";
    private const string ToolsName = "Tools";
    private const string SystemPromptName = "SystemPrompt";

    /// <summary>类型名。</summary>
    public NodeKind Kind => LlmNodeTypes.Name;

    /// <summary>派发 run，输入输出端口取值域由模型产出的形态决定。</summary>
    public NodeTypeShape Shape { get; } = new()
    {
        DispatchesRuns = true,
        InputTypes = [PortType.Text, PortType.Context, PortType.Signal],
        OutputTypes = [PortType.Text, PortType.WorkItems, PortType.Context],
    };

    /// <summary>参数段声明的字段与取值形态。</summary>
    public IReadOnlyList<ParameterField> Fields { get; } =
    [
        new(PromptName, ParameterValueKind.Text),
        new(ToolsName, ParameterValueKind.Texts),
        new(SystemPromptName, ParameterValueKind.Texts),
    ];

    /// <summary>Llm 类型的规则：执行上限、输出校验与按条目展开的组合约束。</summary>
    public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
    {
        ExecutableSpec execution = node.Execution!;
        if (execution.MaxRuns is { } limit && limit < 1)
        {
            fail(scope.Node(node.Name.Value, "MaxRuns 必须是正整数。"));
        }

        if (execution.Validate is not null)
        {
            if (!node.Outputs.Any(port => port.Type == PortType.Text))
            {
                fail(scope.Node(node.Name.Value, "声明 Validate 的节点必须声明 Text 输出端口，校验对象是端口产出。"));
            }

            if (execution.Mode == NodeMode.PerItem)
            {
                fail(scope.Node(node.Name.Value, "声明 Validate 的执行节点不能按条目展开，必须是整节点执行。"));
            }
        }

        // Signal 输入只作驱动、不承载实例内容，按条目展开时仍成立；Context 输入没有实例映射
        if (execution.Mode == NodeMode.PerItem && node.Inputs.Any(port => port.Type == PortType.Context))
        {
            fail(scope.Node(node.Name.Value, "声明 Context 输入端口的执行节点不能按条目展开，必须是整节点执行。"));
        }
    }

    /// <summary>读出参数段：能力路径与系统指令块的规则在此报出，形状与未知字段由文件层报出。</summary>
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
