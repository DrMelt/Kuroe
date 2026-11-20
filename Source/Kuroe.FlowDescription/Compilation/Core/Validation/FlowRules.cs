using Kuroe.FlowDescription.Description.Shared.Reporting;
using ErrorOr;
using Kuroe.FlowDescription.Definition.Shared.Validation;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.FlowDescription.Compilation.Validation;

/// <summary>流程层校验规则：开始端口与节点树。引用解析、输入绑定与图规则在编译后的图上判定。</summary>
public static class FlowRules
{
    /// <summary>校验一条流程的定义结构。类型规则由节点持有的类型描述承担。</summary>
    public static void Validate(FlowDefinition flow, List<Error> errors)
    {
        CheckStart(flow, errors);
        CheckTree(flow.RootNode, NodeScope.Flow(flow.Name.Value), errors);
    }

    /// <summary>开始端口的规则：开始端口可以没有。动态端口只能是 Text 或 Signal，常量端口可以使用任意类型。
    /// Default 只用于承载内容的开始端口，是提交输入的预填值与省略时的回退值，不能与 Value 同时使用；不承载内容的常量开始端口必须写 Value。
    /// 承载内容的常量端口没有 Value 与宿主给值时由提交输入给值，宿主给值的端口由宿主开始输入表在提交时确定。</summary>
    private static void CheckStart(FlowDefinition flow, List<Error> errors)
    {
        var names = new HashSet<PortName>();
        foreach (Port port in flow.StartPorts)
        {
            if (!PortTypes.Supports(port.Type, port.Mode))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"开始端口 {port.Name} 的类型 {port.Type} 不支持模式 {port.Mode}。"));
            }

            if (port.Mode != PortMode.Constant
                && port.Type.Semantics is not (PortSemantics.Text or PortSemantics.Signal))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"开始端口 {port.Name} 的动态类型只能是 Text 或 Signal，常量端口可以使用任意类型。"));
            }

            if (!names.Add(port.Name))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 重复。"));
            }

            if (port.Ref is not null)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 不能写 Ref。"));
            }

            if (port.Trigger)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 不能使用触发。"));
            }

            if (port.Value is { } value && string.IsNullOrWhiteSpace(value))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 的 Value 不能为空。"));
            }

            if (port.Value is not null && port.Default is not null)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 不能同时写 Value 与 Default。"));
            }

            if (port.Default is { } defaultValue && string.IsNullOrWhiteSpace(defaultValue))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 的 Default 不能为空。"));
            }

            if (port.Default is not null && !PortRules.CarriesContent(port.Type))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 的 Default 只用于承载内容的端口。"));
            }

            if (port.IsConstant && !PortRules.CarriesContent(port.Type) && port.Value is null)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"不承载内容的常量开始端口 {port.Name} 必须写 Value。"));
            }
        }
    }

    /// <summary>同一容器内的直接子节点标识唯一：重复会让按标识解析的引用落点不定。</summary>
    private static void CheckSiblings(IReadOnlyList<NodeSpec> children, NodeScope scope, List<Error> errors)
    {
        var seen = new HashSet<NodeLabel>();
        foreach (NodeSpec child in children.Where(child => !seen.Add(child.Label)))
        {
            errors.Add(scope.Node(child.Label.Value, "同一容器内的节点标签重复。"));
        }
    }

    /// <summary>递归校验标识、容器形状、输入绑定与统一输出端口。引用解析与图规则在展平后的图上校验。</summary>
    private static void CheckTree(
        NodeSpec node,
        NodeScope scope,
        List<Error> errors)
    {
        // 单节点不变量由节点规则统一判定，这里只补跨节点与阶段规则
        string label = string.IsNullOrWhiteSpace(node.Label.Value) ? "(未命名)" : node.Label.Value;
        node.Check(NodeRuleContext.FlowDefinition, message =>
            errors.Add(scope.Node(label, message)));

        foreach (Port output in node.Outputs.Where(output => output.Default is not null))
        {
            errors.Add(scope.Node(label, $"输出端口 {output.Name} 不能写 Default，Default 只用于开始端口。"));
        }

        if (node.Nodes is { Count: > 0 } children)
        {
            if (node.Type?.Shape is { Organizes: true })
            {
                node.Type.Validate(node, scope, errors.Add);
            }

            CheckSiblings(children, scope, errors);

            foreach (NodeSpec child in children)
            {
                CheckTree(child, scope, errors);
            }
        }
        else if (node.Spec is not null)
        {
            if (node.Type is not { } definition)
            {
                errors.Add(scope.Node(node.Label.Value, "执行节点必须写类型。"));
            }
            else if (definition.Shape is { Organizes: false })
            {
                definition.Validate(node, scope, errors.Add);
            }
        }
        else if (node.Use is null)
        {
            errors.Add(scope.Node(node.Label.Value, "节点必须声明执行配置或子节点。"));
        }
    }
}
