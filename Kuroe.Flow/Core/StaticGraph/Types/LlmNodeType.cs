using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.RuntimeNodes;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Files;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flow.StaticGraph.Types;

/// <summary>Llm 类型：由模型执行并交回端口产出，引擎内置。</summary>
internal sealed class LlmNodeType : INodeType, INodeParameterJson
{
    private const string PromptName = "Prompt";
    private const string ToolsName = "Tools";
    private const string SystemPromptName = "SystemPrompt";

    /// <summary>类型名。</summary>
    public NodeKind Kind => BuiltinNodeKinds.Llm;

    /// <summary>派发 run，输入输出端口取值域由模型产出的形态决定。</summary>
    public NodeTypeShape Shape { get; } = new()
    {
        DispatchesRuns = true,
        InputTypes = [PortType.Text, PortType.Context, PortType.Signal],
        OutputTypes = [PortType.Text, PortType.WorkItems, PortType.Context],
    };

    /// <summary>类型参数的文件形状。</summary>
    public INodeParameterJson Parameters => this;

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

    /// <summary>Llm 类型的运行期对象。</summary>
    public NodeRuntime Create(GraphNode node, INodeContext context) => new RuntimeExecutable((ExecutableNode)node);

    /// <summary>读出参数段：不认识的键与形状不符的值经 fail 报出。</summary>
    public INodeParameters? Read(
        NodeScope scope,
        NodeName node,
        IReadOnlyDictionary<string, JsonElement> values,
        Action<Error> fail)
    {
        void Report(string message) => fail(scope.Node(node.Value, message));

        foreach (string name in values.Keys.Where(name => name is not (PromptName or ToolsName or SystemPromptName)))
        {
            Report($"字段 {name} 不属于 {Kind.Value} 节点。");
        }

        IReadOnlyList<string> tools = ParameterJson.ReadTexts(values, ToolsName, Report);
        foreach (string tool in tools)
        {
            if (CapabilityPath.InvalidReason(tool) is { } reason)
            {
                Report(reason);
            }
        }

        IReadOnlyList<string> systemPrompt = ParameterJson.ReadTexts(values, SystemPromptName, Report);
        foreach (string _ in systemPrompt.Where(string.IsNullOrWhiteSpace))
        {
            Report("系统指令块不能为空。");
        }

        return new LlmParameters
        {
            Prompt = ParameterJson.ReadText(values, PromptName, Report),
            Tools = [.. tools.Select(tool => new CapabilityPath(tool))],
            SystemPrompt = systemPrompt,
        };
    }

    /// <summary>写回参数段：只写有值的字段。</summary>
    public void Write(INodeParameters? parameters, IDictionary<string, JsonElement> target)
    {
        if (parameters is not LlmParameters llm)
        {
            return;
        }

        if (llm.Prompt is { Length: > 0 } prompt)
        {
            target[PromptName] = ParameterJson.Text(prompt);
        }

        if (llm.Tools.Count > 0)
        {
            target[ToolsName] = ParameterJson.Texts(llm.Tools.Select(tool => tool.Value));
        }

        if (llm.SystemPrompt.Count > 0)
        {
            target[SystemPromptName] = ParameterJson.Texts(llm.SystemPrompt);
        }
    }
}
