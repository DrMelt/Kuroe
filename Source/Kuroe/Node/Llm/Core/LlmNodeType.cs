using ErrorOr;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Tool;

namespace Kuroe.Node.Llm;

/// <summary>Llm 类型：由模型执行并交回端口产出。运行期对象由引擎按形状建立，类型专属声明封在实例里。</summary>
public sealed class LlmNodeType(LlmParameters? parameters, IToolContractCatalog? contracts = null) : INodeType, INodeTypeValidation
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public LlmParameters? Parameters { get; } = parameters;

    /// <summary>装载期可用的工具契约，未经过工具装配时为空。</summary>
    private IToolContractCatalog? Contracts { get; } = contracts;

    /// <summary>类型名。</summary>
    public NodeKind Kind => LlmNodeTypes.Name;

    /// <summary>Llm 类型的静态形状。</summary>
    public NodeTypeShape Shape => LlmNodeTypes.Shape;

    /// <summary>模型名静态输入端口。</summary>
    public PortName? ModelPort => LlmNodeTypes.ModelPort;

    /// <summary>节点声明的工具与系统指令块，供信息查询展示。</summary>
    public IReadOnlyList<NodeAttribute> Attributes
    {
        get
        {
            List<NodeAttribute> attributes = [];
            if (Parameters?.Tools is { Count: > 0 } tools)
            {
                attributes.Add(new NodeAttribute("工具", string.Join("、", tools)));
            }

            if (Parameters?.SystemPrompt is { Count: > 0 } blocks)
            {
                attributes.Add(new NodeAttribute("系统指令", $"{blocks.Count} 块"));
            }

            return attributes;
        }
    }

    /// <summary>Llm 类型的规则：执行上限与输出校验的组合约束。</summary>
    public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
    {
        ExecutableSpec spec = node.Spec!;
        if (spec.MaxExecutions is { } limit && limit < 1)
        {
            fail(scope.Node(node.Label.Value, "MaxExecutions 必须是正整数。"));
        }

        if (node.FindInput(LlmNodeTypes.ModelPort) is not { IsStatic: true, Role: InputRole.Configuration } model)
        {
            fail(scope.Node(node.Label.Value,
                $"Llm 节点必须声明静态输入端口 {LlmNodeTypes.ModelPort.Value}，作为执行使用的模型配置。"));
        }
        else if (model.Type != PortTypes.Text)
        {
            fail(scope.Node(node.Label.Value,
                $"Llm 节点的模型输入 {LlmNodeTypes.ModelPort.Value} 类型只能是 Text。"));
        }

        if (spec.Validate is not null
            && !node.Outputs.Any(port => !port.IsConstant && PortRules.CarriesText(port.Type)))
        {
            fail(scope.Node(node.Label.Value, "声明 Validate 的节点必须声明文本语义的输出端口，校验对象是端口产出。"));
        }

        ToolPortRules.Validate(Parameters, node, Contracts, scope, fail);
    }

    /// <summary>本轮要做的事：节点要求在请求最前，随后是轮次。</summary>
    public string Instruction(ExecutionInput input)
    {
        List<string> instruction = [];
        if (Parameters?.Prompt is { Length: > 0 } prompt)
        {
            instruction.Add(prompt);
        }

        if (input.ExecutionCount > 1)
        {
            instruction.Add($"这是第 {input.ExecutionCount} 轮实施。");
        }

        return string.Join('\n', instruction);
    }

    /// <summary>本轮系统指令：节点声明的恒定块，都没有时为空。</summary>
    public string? SystemPrompt() =>
        Parameters?.SystemPrompt is { Count: > 0 } blocks ? string.Join('\n', blocks) : null;

    /// <summary>本轮可用的工具路径：节点声明的白名单，加按需交回输出端口附上的交回能力，以及所有执行节点都有的上报与查看通道。</summary>
    public IReadOnlyList<ToolPath> ToolPaths(IReadOnlyList<Port> outputs)
    {
        List<ToolPath> paths = [.. Parameters?.Tools ?? []];
        if (outputs.Any(port => PortRules.RequiresSubmission(port.Type)))
        {
            paths.Add(new ToolPath(ContractTools.Port));
        }

        paths.Add(new ToolPath(ContractTools.CannotComplete));
        paths.Add(new ToolPath(ContractTools.Status));

        return paths;
    }
}
