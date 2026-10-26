using ErrorOr;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Compiler.Description;

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
        List<ModelDefinition> models =
        [
            .. flow.Models.Select(model => new ModelDefinition
            {
                Name = new ModelRef(model.Name),
                Model = string.IsNullOrWhiteSpace(model.Model) ? null : model.Model.Trim(),
                Runtime = model.Runtime,
            }),
        ];

        ErrorOr<IReadOnlyList<Port>> startOutputs = ToPorts(flow.StartOutputs, scope, StartNode.ReservedLabel.Value, allowValue: true);
        if (startOutputs.IsError)
        {
            errors.AddRange(startOutputs.ErrorsOrEmptyList);
            return startOutputs.ErrorsOrEmptyList;
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

        Dictionary<PortName, string> hostStartInputs = [];
        foreach ((string port, string input) in flow.HostStartInputs)
        {
            hostStartInputs[new PortName(port)] = input;
        }

        return new FlowDefinition(name, flow.Description, models, startOutputs.Value, hostStartInputs, root.Value);
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

        ErrorOr<OutputValidation?> validation = ToValidation(document.Validate);
        if (validation.IsError)
        {
            errors.AddRange(validation.ErrorsOrEmptyList);
        }

        ErrorOr<Dictionary<PortName, PortRef>?> bindings = ToBindings(document.In);
        if (bindings.IsError)
        {
            errors.AddRange(bindings.ErrorsOrEmptyList);
        }

        ErrorOr<Dictionary<ModelRef, ModelRef>?> models = ToModelBindings(document.Models, scope, label);
        if (models.IsError)
        {
            errors.AddRange(models.ErrorsOrEmptyList);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        return NodeSpec.Reference(new NodeLabel(label), new NodeName(document.Use)) with
        {
            From = from.Value,
            Validate = validation.Value,
            MaxExecutions = document.MaxExecutions,
            In = bindings.Value,
            Model = ToModel(document.Model),
            Models = models.Value,
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

        IReadOnlyList<Port> outputs = shape.Organizes
            ? []
            : ToOutputs(document, shape, inputs.Value, scope, label, errors);

        ErrorOr<Dictionary<PortName, ContainerOutput>?> containerOutputs = shape.Organizes
            ? ToContainerOutputs(document.Outputs, scope, label)
            : (Dictionary<PortName, ContainerOutput>?)null;
        if (containerOutputs.IsError)
        {
            errors.AddRange(containerOutputs.ErrorsOrEmptyList);
        }

        ErrorOr<IReadOnlyList<FromGroup>> from = ToFrom(document.From, scope, label);
        if (from.IsError)
        {
            errors.AddRange(from.ErrorsOrEmptyList);
        }

        ErrorOr<OutputValidation?> validation = ToValidation(document.Validate);
        if (validation.IsError)
        {
            errors.AddRange(validation.ErrorsOrEmptyList);
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
            Outputs = shape.Organizes ? [] : outputs,
            ContainerOutputs = containerOutputs.Value,
            From = from.Value,
            Model = ToModel(document.Model),
            ModelSlots = document.ModelSlots is { Count: > 0 } slots ? [.. slots.Select(slot => new ModelRef(slot))] : null,
            Nodes = children,
            Validate = validation.Value,
            MaxExecutions = shape.Organizes ? document.MaxExecutions : null,
            Spec = shape.Organizes
                ? null
                : new ExecutableSpec { Validate = validation.Value, MaxExecutions = document.MaxExecutions },
        };
    }

    /// <summary>检查文档字段与节点形状的互斥：定义条目不写输入绑定，容器不写执行字段，模型槽位只属于节点库容器定义。</summary>
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

            if (document.From.Count > 0)
            {
                Fail("容器节点不是执行节点，不支持 From。");
            }

            if (document.Model is not null)
            {
                Fail("容器节点不是执行节点，不支持模型选择。");
            }

            if (document.In is not null)
            {
                Fail("容器节点不是执行节点，不支持输入端口绑定。");
            }

            if (document.Inputs.Count > 0 && !scope.IsLibrary)
            {
                Fail("容器节点不能声明输入端口，端口声明属于节点库容器定义。");
            }

            if (document.Validate is not null)
            {
                Fail("容器节点不是执行节点，不支持 Validate。");
            }

            if (document.HasModelBindings)
            {
                Fail(scope.IsLibrary
                    ? "容器定义的模型槽位写槽位名数组。"
                    : "内联容器不声明模型槽位与模型绑定，槽位声明只属于节点库容器定义。");
            }
            else if (document.ModelSlots is not null && !scope.IsLibrary)
            {
                Fail("内联容器不声明模型槽位与模型绑定，槽位声明只属于节点库容器定义。");
            }

            return;
        }

        if (document.Nodes is { Count: > 0 })
        {
            Fail($"{document.Kind} 节点不能声明子节点，子节点只属于容器。");
        }

        if (document.In is not null)
        {
            Fail("执行节点不写输入端口绑定，接线一律经 From。");
        }

        if (document.HasModelBindings || document.ModelSlots is not null)
        {
            Fail("执行节点不声明模型槽位与模型绑定，槽位声明只属于容器定义。");
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

            result.Add(new InputPort(name.Value, entry.Type ?? PortTypes.Text, ToDescription(entry.Description), entry.Default));
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
        if (shape.DerivesOutputsFromInputs)
        {
            if (document.Outputs.Count > 0)
            {
                errors.Add(scope.Node(nodeLabel, $"{document.Kind}节点不声明输出端口，对外端口取自身输入端口。"));
            }

            return ForwardedPorts(inputs);
        }

        ErrorOr<IReadOnlyList<Port>> parsed = ToPorts(document.Outputs, scope, nodeLabel, allowValue: false);
        if (parsed.IsError)
        {
            errors.AddRange(parsed.ErrorsOrEmptyList);

            return [];
        }

        return parsed.Value;
    }

    private static IReadOnlyList<Port> ForwardedPorts(IReadOnlyList<InputPort> inputs) =>
        [.. inputs.Select(input => new Port(input.Name, input.Type, input.Description))];

    private static ErrorOr<IReadOnlyList<Port>> ToPorts(
        IReadOnlyList<PortDocument> entries,
        NodeScope scope,
        string nodeLabel,
        bool allowValue)
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

            if (entry.Ref is not null)
            {
                errors.Add(scope.Node(nodeLabel, "执行节点的输出端口条目写 Name 与 Type，不写 Ref。"));
                continue;
            }

            if (entry.Type is not { } type)
            {
                errors.Add(scope.Node(nodeLabel, "输出端口的 Type 不能省略，必须是已注册的端口类型名。"));
                continue;
            }

            if (!allowValue && entry.Value is not null)
            {
                errors.Add(scope.Node(nodeLabel, "输出端口不认识键 Value，Value 只属于开始端口。"));
                continue;
            }

            result.Add(new Port(name.Value, type, ToDescription(entry.Description), allowValue ? entry.Value : null));
        }

        return errors.Count > 0 ? errors : result;
    }

    private static ErrorOr<Dictionary<PortName, ContainerOutput>?> ToContainerOutputs(
        IReadOnlyList<PortDocument> entries,
        NodeScope scope,
        string nodeLabel)
    {
        if (entries.Count == 0)
        {
            return (Dictionary<PortName, ContainerOutput>?)null;
        }

        Dictionary<PortName, ContainerOutput> result = [];
        List<Error> errors = [];
        foreach (PortDocument entry in entries)
        {
            ErrorOr<PortName> name = PortName.Create(entry.Name);
            if (name.IsError)
            {
                errors.AddRange(name.ErrorsOrEmptyList);
                continue;
            }

            if (entry.Type is not null)
            {
                errors.Add(scope.Node(nodeLabel, "容器输出端口不认识键 Type，条目写 Name、Ref 与 Description。"));
                continue;
            }

            if (entry.Ref is not { } refText)
            {
                errors.Add(scope.Node(nodeLabel, "容器输出端口的 Ref 不能省略，写 成员@端口。"));
                continue;
            }

            ErrorOr<PortRef> reference = ParsePortRef(refText);
            if (reference.IsError)
            {
                errors.AddRange(reference.ErrorsOrEmptyList);
                continue;
            }

            if (!result.TryAdd(name.Value, new ContainerOutput(reference.Value, ToDescription(entry.Description))))
            {
                errors.Add(scope.Node(nodeLabel, $"容器输出端口 {name.Value.Value} 重复。"));
            }
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

                links.Add(new FromLink(input.Value, reference.Value));
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

    private static ErrorOr<Dictionary<PortName, PortRef>?> ToBindings(IReadOnlyDictionary<string, string>? bindings)
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

    private static ErrorOr<Dictionary<ModelRef, ModelRef>?> ToModelBindings(
        IReadOnlyDictionary<string, string>? models,
        NodeScope scope,
        string nodeLabel)
    {
        if (models is not { Count: > 0 })
        {
            return (Dictionary<ModelRef, ModelRef>?)null;
        }

        Dictionary<ModelRef, ModelRef> result = [];
        List<Error> errors = [];
        foreach ((string key, string value) in models)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add(scope.Node(nodeLabel, "模型绑定键与值都不能为空。"));
                continue;
            }

            result[new ModelRef(key)] = new ModelRef(value);
        }

        return errors.Count > 0 ? errors : result;
    }

    private static ErrorOr<OutputValidation?> ToValidation(ValidationDocument? validation)
    {
        if (validation is null)
        {
            return (OutputValidation?)null;
        }

        if (string.IsNullOrWhiteSpace(validation.Predicate))
        {
            return Error.Validation(FlowErrorCodes.FlowNode, "Validate.Predicate 不能为空。");
        }

        string? matched = Enum.GetNames<ValidationPredicate>()
            .FirstOrDefault(name => string.Equals(name, validation.Predicate, StringComparison.OrdinalIgnoreCase));

        return matched is not null
            ? new OutputValidation(Enum.Parse<ValidationPredicate>(matched), validation.Argument)
            : Error.Validation(FlowErrorCodes.FlowNode,
                $"Validate.Predicate 应为 NonEmpty、TextContains、TextNot、TextEquals 或 Pattern，收到 {validation.Predicate}。");
    }

    private static ModelRef? ToModel(string? model) =>
        string.IsNullOrWhiteSpace(model) ? null : new ModelRef(model);

    private static string? ToDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}