using ErrorOr;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.StaticGraph.Flow;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.Compilation.Description;

/// <summary>描述文档到授权模型的构造：字段映射、缺省值与形状驱动的结构，不判断模型不变量。</summary>
internal sealed class FlowModelBuilder(INodeTypeDescriptionRegistry types)
{
    /// <summary>把文档里的流程逐条构造成授权模型，错误一次给全。</summary>
    public ErrorOr<IReadOnlyList<FlowDefinition>> Build(FlowDocument document)
    {
        List<FlowDefinition> flows = [];
        List<Error> errors = [];

        foreach (FlowDocumentFlow flow in document.Flows)
        {
            ErrorOr<FlowName> name = FlowName.Create(flow.Name);
            if (name.IsError)
            {
                errors.AddRange(name.ErrorsOrEmptyList);
                continue;
            }

            List<Error> flowErrors = [];
            ErrorOr<FlowDefinition> built = BuildFlow(name.Value, flow, flowErrors);
            if (built.IsSuccess && flowErrors.Count == 0)
            {
                flows.Add(built.Value);
            }
            else
            {
                errors.AddRange(flowErrors);
            }
        }

        return errors.Count > 0 ? errors : flows;
    }

    /// <summary>把文档顶层的节点库定义构造成模型定义，错误一次给全。</summary>
    public ErrorOr<IReadOnlyList<NodeSpec>> BuildLibrary(IReadOnlyList<NodeDefinitionDocument> nodes)
    {
        List<NodeSpec> definitions = [];
        List<Error> errors = [];
        foreach (NodeDefinitionDocument node in nodes)
        {
            List<Error> nodeErrors = [];
            ErrorOr<NodeSpec> built = ToDefinition(node, NodeScope.Library, nodeErrors);
            if (built.IsSuccess && nodeErrors.Count == 0)
            {
                definitions.Add(built.Value);
            }
            else
            {
                errors.AddRange(nodeErrors);
            }
        }

        return errors.Count > 0 ? errors : definitions;
    }

    private ErrorOr<FlowDefinition> BuildFlow(FlowName name, FlowDocumentFlow flow, List<Error> errors)
    {
        NodeScope scope = NodeScope.Flow(name.Value);

        ErrorOr<IReadOnlyList<Port>> ports = ToPorts(flow.StartPorts, scope, "开始");
        if (ports.IsError)
        {
            errors.AddRange(ports.ErrorsOrEmptyList);
            return ports.ErrorsOrEmptyList;
        }

        if (flow.Root is null)
        {
            errors.Add(FlowErrors.Body(name.Value, "流程缺少根节点。"));

            return errors;
        }

        ErrorOr<NodeSpec> root = ToNode(flow.Root, scope, errors);
        if (root.IsError)
        {
            return root.ErrorsOrEmptyList;
        }

        return new FlowDefinition(name, flow.Description, ports.Value, root.Value);
    }

    private ErrorOr<NodeSpec> ToNode(NodeDocument document, NodeScope scope, List<Error> errors) => document switch
    {
        NodeReferenceDocument reference => ToReference(reference, scope, errors),
        NodeDefinitionDocument definition => ToDefinition(definition, scope, errors),
        _ => scope.Node(string.Empty, "不认识的节点描述。"),
    };

    private static ErrorOr<NodeSpec> ToReference(NodeReferenceDocument document, NodeScope scope, List<Error> errors)
    {
        string label = document.Label;
        ErrorOr<IReadOnlyList<FromGroup>> from = ToFrom(document.From, scope, label);
        if (from.IsError)
        {
            errors.AddRange(from.ErrorsOrEmptyList);
        }

        ErrorOr<Dictionary<PortName, PortRef>?> bindings = ToBindings(document.Bindings);
        if (bindings.IsError)
        {
            errors.AddRange(bindings.ErrorsOrEmptyList);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        return NodeSpec.Reference(new NodeLabel(label), new NodeName(document.Use)) with
        {
            From = from.Value,
            Emission = document.Emission,
            MaxExecutions = document.MaxExecutions,
            Bindings = bindings.Value,
        };
    }

    private ErrorOr<NodeSpec> ToDefinition(NodeDefinitionDocument document, NodeScope scope, List<Error> errors)
    {
        string label = document.Name;
        if (string.IsNullOrWhiteSpace(document.Kind))
        {
            errors.Add(scope.Node(label,
                $"节点必须声明 Kind，可用类型名：{string.Join("、", types.Kinds.Select(item => item.Value))}。"));

            return errors;
        }

        if (types.Find(document.Kind) is not { } description)
        {
            errors.Add(scope.Node(label,
                $"节点类型名 {document.Kind} 未注册，可用类型名：{string.Join("、", types.Kinds.Select(item => item.Value))}。"));

            return errors;
        }

        NodeTypeShape shape = description.Shape;
        CheckDocumentShape(document, shape, scope, errors);

        ErrorOr<IReadOnlyList<InputPort>> inputs = ToInputs(document.Inputs, scope, label);
        if (inputs.IsError)
        {
            errors.AddRange(inputs.ErrorsOrEmptyList);

            return errors;
        }

        IReadOnlyList<Port> outputs;
        if (shape.Organizes)
        {
            ErrorOr<IReadOnlyList<Port>> parsed = ToPorts(document.Outputs, scope, label);
            if (parsed.IsError)
            {
                errors.AddRange(parsed.ErrorsOrEmptyList);
                outputs = [];
            }
            else
            {
                outputs = parsed.Value;
            }
        }
        else
        {
            outputs = ToOutputs(document, shape, inputs.Value, scope, label, errors);
        }

        ErrorOr<IReadOnlyList<FromGroup>> from = ToFrom(document.From, scope, label);
        if (from.IsError)
        {
            errors.AddRange(from.ErrorsOrEmptyList);
        }

        ErrorOr<Dictionary<PortName, PortRef>?> bindings = ToBindings(document.Bindings);
        if (bindings.IsError)
        {
            errors.AddRange(bindings.ErrorsOrEmptyList);
        }

        INodeType? type = ParameterDocumentReader.Read(description, document.Parameters, scope, new NodeLabel(label), errors.Add);

        List<NodeSpec>? children = null;
        if (document.Nodes is { Count: > 0 } nodes)
        {
            children = [];
            foreach (NodeDocument child in nodes)
            {
                ErrorOr<NodeSpec> built = ToNode(child, scope, errors);
                if (built.IsSuccess)
                {
                    children.Add(built.Value);
                }
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        return new NodeSpec
        {
            Name = new NodeName(label),
            Label = new NodeLabel(label),
            Type = type,
            Inputs = inputs.Value,
            Outputs = outputs,
            From = from.Value,
            Bindings = bindings.Value,
            Nodes = children,
            MaxExecutions = shape.Organizes ? document.MaxExecutions : null,
            Spec = shape.Organizes
                ? null
                : new ExecutableSpec
                {
                    Emission = document.Emission ?? NodeEmission.Repeated,
                    MaxExecutions = document.MaxExecutions,
                },
        };
    }

    /// <summary>检查文档字段与节点形状的互斥：库定义不写输入绑定，容器不写执行字段。</summary>
    private static void CheckDocumentShape(
        NodeDefinitionDocument document,
        NodeTypeShape shape,
        NodeScope scope,
        List<Error> errors)
    {
        void Fail(string message) => errors.Add(scope.Node(document.Name, message));

        if (shape.Organizes)
        {
            if (document.Nodes is not { Count: > 0 })
            {
                Fail("容器节点必须声明子节点。");
            }

            if (document.Bindings is not null && scope.IsLibrary)
            {
                Fail("库节点定义不接线，不能声明输入绑定。");
            }

            if (document.Emission is not null)
            {
                Fail("容器节点不是执行节点，不支持 Emission。");
            }

            return;
        }

        if (document.Nodes is { Count: > 0 })
        {
            Fail($"{document.Kind} 节点不能声明子节点，子节点只属于容器。");
        }
    }

    private static ErrorOr<IReadOnlyList<InputPort>> ToInputs(
        IReadOnlyList<InputPortDocument> entries,
        NodeScope scope,
        string nodeLabel)
    {
        List<InputPort> result = [];
        List<Error> errors = [];
        foreach (InputPortDocument entry in entries)
        {
            ErrorOr<PortName> name = PortName.Create(entry.Name);
            if (name.IsError)
            {
                errors.Add(scope.Node(nodeLabel, name.FirstError.Description));
                continue;
            }

            result.Add(new InputPort(
                name.Value,
                entry.Type ?? PortTypes.Text,
                ToDescription(entry.Description),
                entry.Default,
                entry.Mode,
                entry.Role));
        }

        return errors.Count > 0 ? errors : result;
    }

    private static IReadOnlyList<Port> ToOutputs(
        NodeDefinitionDocument document,
        NodeTypeShape shape,
        IReadOnlyList<InputPort> inputs,
        NodeScope scope,
        string nodeLabel,
        List<Error> errors)
    {
        switch (shape.Outputs)
        {
            case OutputDeclaration.Given given:
                if (document.Outputs.Count > 0)
                {
                    errors.Add(scope.Node(nodeLabel, $"{document.Kind}节点不声明输出端口，对外端口由类型给出。"));
                }

                return given.Ports;

            case OutputDeclaration.DerivedFromInputs:
                if (document.Outputs.Count > 0)
                {
                    errors.Add(scope.Node(nodeLabel, $"{document.Kind}节点不声明输出端口，对外端口取自身输入端口。"));
                }

                return ForwardedPorts(inputs);

            case OutputDeclaration.Declared:
                break;

            default:
                throw new InvalidOperationException($"不认识的输出声明 {shape.Outputs.GetType().Name}。");
        }

        ErrorOr<IReadOnlyList<Port>> parsed = ToPorts(document.Outputs, scope, nodeLabel);
        if (parsed.IsError)
        {
            errors.AddRange(parsed.ErrorsOrEmptyList);

            return [];
        }

        return parsed.Value;
    }

    /// <summary>派生对外端口：取自身非静态输入端口，只转发内容。批准事件不放行输入的触发值，输出不声明触发模式。</summary>
    private static IReadOnlyList<Port> ForwardedPorts(IReadOnlyList<InputPort> inputs) =>
        [.. inputs.Where(input => !input.IsStatic)
            .Select(input => new Port(
                input.Name,
                input.Type,
                input.Description,
                mode: PortMode.Plain))];

    private static ErrorOr<IReadOnlyList<Port>> ToPorts(
        IReadOnlyList<PortDocument> entries,
        NodeScope scope,
        string nodeLabel)
    {
        List<Port> result = [];
        List<Error> errors = [];
        foreach (PortDocument entry in entries)
        {
            ErrorOr<PortName> name = PortName.Create(entry.Name);
            if (name.IsError)
            {
                errors.AddRange(name.ErrorsOrEmptyList);
                continue;
            }

            if (entry.Value is not null && entry.Ref is not null)
            {
                errors.Add(scope.Node(nodeLabel, $"输出端口 {name.Value.Value} 不能同时写 Value 与 Ref。"));
                continue;
            }

            PortRef? reference = null;
            if (entry.Ref is { } refText)
            {
                ErrorOr<PortRef> parsed = ParsePortRef(refText);
                if (parsed.IsError)
                {
                    errors.Add(scope.Node(nodeLabel, parsed.FirstError.Description));
                    continue;
                }

                reference = parsed.Value;
            }

            result.Add(new Port(
                name.Value,
                entry.Type ?? PortTypes.Text,
                ToDescription(entry.Description),
                entry.Value,
                reference,
                entry.Mode,
                entry.Default));
        }

        return errors.Count > 0 ? errors : result;
    }

    private static ErrorOr<IReadOnlyList<FromGroup>> ToFrom(
        IReadOnlyList<FromGroupDocument> entries,
        NodeScope scope,
        string nodeLabel)
    {
        if (entries.Count == 0)
        {
            return Array.Empty<FromGroup>();
        }

        List<FromGroup> result = [];
        List<Error> errors = [];
        foreach (FromGroupDocument entry in entries)
        {
            List<FromLink> links = [];
            foreach (FromLinkDocument link in entry.Links)
            {
                ErrorOr<PortName> input = PortName.Create(link.Input);
                if (input.IsError)
                {
                    errors.AddRange(input.ErrorsOrEmptyList);
                    continue;
                }

                ErrorOr<PortRef> reference = ParsePortRef(link.Ref);
                if (reference.IsError)
                {
                    errors.AddRange(reference.ErrorsOrEmptyList);
                    continue;
                }

                links.Add(new FromLink(
                    input.Value,
                    reference.Value,
                    link.Condition ?? TriggerCondition.Positive));
            }

            if (links.Count == 0)
            {
                errors.Add(scope.Node(nodeLabel, "From 的组至少要有一条连接。"));
                continue;
            }

            result.Add(new FromGroup(links));
        }

        return errors.Count > 0 ? errors : result;
    }

    private static ErrorOr<PortRef> ParsePortRef(string text)
    {
        if (text.StartsWith('@'))
        {
            ErrorOr<PortName> bound = PortName.Create(text[1..]);
            return bound.IsError ? bound.ErrorsOrEmptyList : PortRef.Bound(bound.Value);
        }

        int at = text.IndexOf('@');
        if (at > 0 && at < text.Length - 1)
        {
            ErrorOr<PortName> port = PortName.Create(text[(at + 1)..]);
            if (port.IsError)
            {
                return port.ErrorsOrEmptyList;
            }

            return PortRef.Named(new NodeLabel(text[..at]), port.Value);
        }

        return Error.Validation(FlowErrorCodes.FlowNode, $"端口引用 {text} 缺少 @，必须写 来源@端口 或 @端口。");
    }

    private static ErrorOr<Dictionary<PortName, PortRef>?> ToBindings(
        IReadOnlyDictionary<string, string>? bindings)
    {
        if (bindings is not { Count: > 0 })
        {
            return (Dictionary<PortName, PortRef>?)null;
        }

        Dictionary<PortName, PortRef> result = [];
        List<Error> errors = [];
        foreach ((string key, string value) in bindings)
        {
            ErrorOr<PortName> port = PortName.Create(key);
            if (port.IsError)
            {
                errors.AddRange(port.ErrorsOrEmptyList);
                continue;
            }

            ErrorOr<PortRef> parsed = ParsePortRef(value);
            if (parsed.IsError)
            {
                errors.AddRange(parsed.ErrorsOrEmptyList);
                continue;
            }

            result[port.Value] = parsed.Value;
        }

        return errors.Count > 0 ? errors : result;
    }

    private static string? ToDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}