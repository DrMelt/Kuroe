using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Tasks;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Tool;

namespace Kuroe.Flow.Node.Llm;

/// <summary>Llm 类型：由模型执行并交回端口产出。运行期对象由引擎按形状建立，类型专属声明封在实例里。</summary>
public sealed class LlmNodeType(LlmParameters? parameters) : INodeType
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public LlmParameters? Parameters { get; } = parameters;

    /// <summary>类型名。</summary>
    public NodeKind Kind => LlmNodeTypes.Name;

    /// <summary>Llm 类型的静态形状。</summary>
    public NodeTypeShape Shape => LlmNodeTypes.Shape;

    /// <summary>Llm 类型的规则：执行上限、输出校验与按条目展开的组合约束。</summary>
    public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
    {
        ExecutableSpec execution = node.Execution!;
        if (execution.MaxRuns is { } limit && limit < 1)
        {
            fail(scope.Node(node.Label.Value, "MaxRuns 必须是正整数。"));
        }

        if (execution.Validate is not null)
        {
            if (!node.Outputs.Any(port => port.Type == PortType.Text))
            {
                fail(scope.Node(node.Label.Value, "声明 Validate 的节点必须声明 Text 输出端口，校验对象是端口产出。"));
            }

            if (execution.Mode == NodeMode.PerItem)
            {
                fail(scope.Node(node.Label.Value, "声明 Validate 的执行节点不能按条目展开，必须是整节点执行。"));
            }
        }

        // Signal 输入只作驱动、不承载实例内容，按条目展开时仍成立；Context 输入没有实例映射
        if (execution.Mode == NodeMode.PerItem && node.Inputs.Any(port => port.Type == PortType.Context))
        {
            fail(scope.Node(node.Label.Value, "声明 Context 输入端口的执行节点不能按条目展开，必须是整节点执行。"));
        }
    }

    /// <summary>本轮要做的事：节点要求在指令最前。</summary>
    public string Compose(RunMaterials materials)
    {
        List<string> instruction = [];
        if (Parameters?.Prompt is { Length: > 0 } prompt)
        {
            instruction.Add(prompt);
        }

        instruction.AddRange(materials.Steps);

        return string.Join('\n', instruction);
    }

    /// <summary>本轮系统指令：恒定块在前，引擎给出的本轮接口说明在后，块间空行分隔。都没有时为空。</summary>
    public string? Instructions(string briefing)
    {
        List<string> blocks = [.. Parameters?.SystemPrompt ?? []];
        if (briefing.Length > 0)
        {
            if (blocks.Count > 0)
            {
                blocks.Add(string.Empty);
            }

            blocks.Add(briefing);
        }

        return blocks.Count == 0 ? null : string.Join('\n', blocks);
    }

    /// <summary>本轮可用的工具路径：节点声明的白名单，加按输出端口附上的契约能力。交回各契约入口的通道所有节点都有。</summary>
    public IReadOnlyList<ToolPath> ToolPaths(IReadOnlyList<Port> outputs)
    {
        List<ToolPath> paths = [.. Parameters?.Tools ?? []];
        if (outputs.Any(port => port.Type == PortType.WorkItems))
        {
            paths.Add(new ToolPath(ContractTools.WorkItems));
        }

        if (outputs.Any(port => port.Type == PortType.Text))
        {
            paths.Add(new ToolPath(ContractTools.Port));
        }

        paths.Add(new ToolPath(ContractTools.Status));

        return paths;
    }
}
