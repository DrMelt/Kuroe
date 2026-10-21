using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Flow.StaticGraph;
using ModelDefinition = Kuroe.Flow.Shared.StaticGraph.Flow.ModelDefinition;

namespace Kuroe.Flow.Json.Files;

/// <summary>流程文件形状到模型的读取：把 DTO 绑成节点定义与流程模型，错误逐条收集。</summary>
static class FlowReader
{
    /// <summary>把文件形状转成节点定义与流程模型，错误逐条收集。</summary>
    internal static ErrorOr<(IReadOnlyList<NodeSpec> Nodes, IReadOnlyList<FlowDefinition> Flows)> ToContent(
        FlowFileDto file,
        NodeTypeFileRegistry files)
    {
        List<NodeSpec> nodes = [];
        List<Error> errors = [];
        foreach (NodeDto dto in file.Nodes)
        {
            ErrorOr<NodeSpec> converted = ToNode(dto, NodeScope.Library, files);
            if (converted.IsError)
            {
                errors.AddRange(converted.ErrorsOrEmptyList);
            }
            else
            {
                nodes.Add(converted.Value);
            }
        }

        List<FlowDefinition> flows = [];
        foreach (FlowDto dto in file.Flows)
        {
            ErrorOr<FlowName> flowName = FlowName.Create(dto.Name ?? string.Empty);
            if (flowName.IsError)
            {
                errors.AddRange(flowName.ErrorsOrEmptyList);

                continue;
            }

            ErrorOr<FlowDefinition> flow = ToFlowDefinition(flowName.Value, dto, files);
            if (flow.IsError)
            {
                errors.AddRange(flow.ErrorsOrEmptyList);

                continue;
            }

            flows.Add(flow.Value);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        return (nodes, flows);
    }

    private static ErrorOr<FlowDefinition> ToFlowDefinition(FlowName name, FlowDto dto, NodeTypeFileRegistry files)
    {
        List<ModelDefinition> models = [];
        foreach (ModelDto model in dto.Models)
        {
            models.Add(new ModelDefinition
            {
                Name = new ModelRef(model.Name ?? string.Empty),
                Model = string.IsNullOrWhiteSpace(model.Model) ? null : model.Model.Trim(),
            });
        }

        if (dto.Nodes.Count == 0)
        {
            return FlowErrors.Body(name.Value, "流程缺少根节点。");
        }

        if (dto.Nodes.Count > 1)
        {
            return FlowErrors.Body(name.Value, "流程只能有一个根节点。");
        }

        ErrorOr<IReadOnlyList<Port>> startOutputs = ToPorts(dto.Start?.Outputs, NodeScope.Flow(name.Value), StartNode.ReservedLabel.Value, allowValue: true);
        if (startOutputs.IsError)
        {
            return startOutputs.ErrorsOrEmptyList;
        }

        ErrorOr<NodeSpec> root = ToNode(dto.Nodes[0], NodeScope.Flow(name.Value), files);
        if (root.IsError)
        {
            return root.ErrorsOrEmptyList;
        }

        return new FlowDefinition(name, dto.Description, models, startOutputs.Value, root.Value);
    }

    /// <summary>装配节点的列表转换，错误逐条收集。</summary>
    private static ErrorOr<IReadOnlyList<NodeSpec>> ToNodes(List<NodeDto> nodes, NodeScope scope, NodeTypeFileRegistry files)
    {
        List<NodeSpec> result = [];
        List<Error> errors = [];

        foreach (NodeDto dto in nodes)
        {
            ErrorOr<NodeSpec> converted = ToNode(dto, scope, files);
            if (converted.IsError)
            {
                errors.AddRange(converted.ErrorsOrEmptyList);
            }
            else
            {
                result.Add(converted.Value);
            }
        }

        return errors.Count > 0 ? errors : result;
    }

    /// <summary>把文件里的节点装配成模型：写有 Use 即引用库定义，否则按 Kind 分容器、执行节点与输入节点。
    /// 定义条目写节点名，引用条目写标签，标识各取条目写下的那一处；未写的字段取安全默认值，合法性交校验。</summary>
    private static ErrorOr<NodeSpec> ToNode(NodeDto dto, NodeScope scope, NodeTypeFileRegistry files)
    {
        if (dto.Use is { Length: > 0 } use)
        {
            if (dto.Name is not null)
            {
                return scope.Node(dto.Name, "被使用的节点写 Label 区分，不写 Name。");
            }

            if (string.IsNullOrWhiteSpace(dto.Label))
            {
                return scope.Node(use, "被使用的节点必须写 Label，它是节点在图里的标识段。");
            }
        }
        else if (dto.Label is not null)
        {
            return scope.Node(dto.Label, "定义条目写节点名 Name，Label 只属于被使用的节点。");
        }

        // 条目的标识：定义条目取节点名文本，引用条目取使用处写下的标签
        string label = (dto.Use is { Length: > 0 } ? dto.Label : dto.Name) ?? string.Empty;

        if (dto.Use is { Length: > 0 })
        {
            List<Error> useErrors = [];
            if (dto.Kind is not null)
            {
                useErrors.Add(scope.Node(label, "引用节点的类型由被引用的库定义决定，不写 Kind。"));
            }

            if (dto.Nodes is { Count: > 0 })
            {
                useErrors.Add(scope.Node(label, "引用成员不能同时声明子节点。"));
            }

            if (dto.Parameters is { Count: > 0 } declaredParameters)
            {
                useErrors.Add(scope.Node(label,
                    $"引用节点不能声明类型参数 {string.Join("、", declaredParameters.Keys)}。"));
            }

            if (dto.Model is not null && HasModels(dto.Models))
            {
                useErrors.Add(scope.Node(label, "引用节点不能同时声明模型与模型绑定。"));
            }

            if (dto.Inputs is { Count: > 0 })
            {
                useErrors.Add(scope.Node(label, "引用节点不能声明输入端口，端口声明属于被引用的定义。"));
            }

            if (dto.Outputs is { Count: > 0 })
            {
                useErrors.Add(scope.Node(label, "引用节点不能声明输出端口。"));
            }

            if (useErrors.Count > 0)
            {
                return useErrors;
            }

            OutputValidation? declareValidation = null;
            if (dto.Validate is { } validateDeclared)
            {
                ErrorOr<ValidationPredicate> parsedPredicate = ParseValidationPredicate(validateDeclared.Predicate);
                if (parsedPredicate.IsError)
                {
                    useErrors.Add(scope.Node(label, parsedPredicate.FirstError.Description));
                    return useErrors;
                }

                declareValidation = new OutputValidation(parsedPredicate.Value, validateDeclared.Argument);
            }

            ErrorOr<Dictionary<PortName, PortRef>?> resolvedIn = ToBindings(dto.In);
            if (resolvedIn.IsError)
            {
                useErrors.AddRange(resolvedIn.ErrorsOrEmptyList);
                return useErrors;
            }

            ErrorOr<Dictionary<ModelRef, ModelRef>?> resolvedModels = ToModelBindings(dto.Models, scope, label);
            if (resolvedModels.IsError)
            {
                useErrors.AddRange(resolvedModels.ErrorsOrEmptyList);
                return useErrors;
            }

            ErrorOr<IReadOnlyList<FromGroup>> resolvedFrom = ToFrom(dto.From, scope, label);
            if (resolvedFrom.IsError)
            {
                return resolvedFrom.ErrorsOrEmptyList;
            }

            return NodeSpec.Reference(new NodeLabel(label), new NodeName(dto.Use)) with
            {
                From = resolvedFrom.Value,
                Validate = declareValidation,
                MaxExecutions = dto.MaxExecutions,
                In = resolvedIn.Value,
                Model = ToModel(dto.Model),
                Models = resolvedModels.Value,
            };
        }

        if (dto.Kind is not { } kind)
        {
            return scope.Node(label,
                $"节点必须声明 Kind，可用类型名：{string.Join("、", files.Kinds.Select(item => item.Value))}。");
        }

        if (files.Find(kind) is not { } declaration)
        {
            return scope.Node(label,
                $"节点类型名 {kind} 未注册，可用类型名：{string.Join("、", files.Kinds.Select(item => item.Value))}。");
        }

        NodeTypeShape declared = declaration.Shape;
        if (declared.Organizes)
        {
            List<Error> containerErrors = [];
            if (dto.From is { Count: > 0 })
            {
                containerErrors.Add(scope.Node(label, "容器节点不是执行节点，不支持 From。"));
            }

            if (dto.Model is not null)
            {
                containerErrors.Add(scope.Node(label, "容器节点不是执行节点，不支持模型选择。"));
            }

            IReadOnlyList<ModelRef>? declaredSlots = null;
            if (dto.Models is { } declaredModels)
            {
                if (!scope.IsLibrary)
                {
                    containerErrors.Add(scope.Node(label, "内联容器不声明模型槽位与模型绑定，槽位声明只属于节点库容器定义。"));
                }
                else if (declaredModels.ValueKind != JsonValueKind.Array)
                {
                    containerErrors.Add(scope.Node(label, "容器定义的模型槽位写槽位名数组。"));
                }
                else
                {
                    ErrorOr<IReadOnlyList<ModelRef>> slots = ToModelSlots(declaredModels, scope, label);
                    if (slots.IsError)
                    {
                        containerErrors.AddRange(slots.ErrorsOrEmptyList);
                    }
                    else
                    {
                        declaredSlots = slots.Value;
                    }
                }
            }

            if (dto.In is { Count: > 0 })
            {
                containerErrors.Add(scope.Node(label, "容器节点不是执行节点，不支持输入端口绑定。"));
            }

            if (dto.Inputs is { Count: > 0 } && !scope.IsLibrary)
            {
                containerErrors.Add(scope.Node(label, "容器节点不能声明输入端口，端口声明属于节点库容器定义。"));
            }

            if (dto.Validate is not null)
            {
                containerErrors.Add(scope.Node(label, "容器节点不是执行节点，不支持 Validate。"));
            }

            IReadOnlyList<NodeSpec> children = [];
            if (dto.Nodes is { Count: > 0 })
            {
                ErrorOr<IReadOnlyList<NodeSpec>> parsedChildren = ToNodes(dto.Nodes, scope, files);
                if (parsedChildren.IsError)
                {
                    containerErrors.AddRange(parsedChildren.ErrorsOrEmptyList);
                }
                else
                {
                    children = parsedChildren.Value;
                }
            }
            else
            {
                containerErrors.Add(scope.Node(label, "容器节点必须声明子节点。"));
            }

            ErrorOr<IReadOnlyList<InputPort>> inputs = ToInputs(dto.Inputs, scope, label);
            if (inputs.IsError)
            {
                containerErrors.AddRange(inputs.ErrorsOrEmptyList);
            }

            ErrorOr<Dictionary<PortName, ContainerOutput>?> outputPorts = ToContainerOutputs(dto.Outputs, scope, label);
            if (outputPorts.IsError)
            {
                containerErrors.AddRange(outputPorts.ErrorsOrEmptyList);
            }

            INodeType containerType = ReadParameters(declaration, dto, new NodeLabel(label), scope, containerErrors);

            if (containerErrors.Count > 0)
            {
                return containerErrors;
            }

            return NodeSpec.Define(new NodeName(dto.Name ?? string.Empty)) with
            {
                Type = containerType,
                Inputs = inputs.Value,
                ContainerOutputs = outputPorts.Value,
                MaxExecutions = dto.MaxExecutions,
                ModelSlots = declaredSlots,
                Nodes = children,
            };
        }

        List<Error> leafErrors = [];
        if (dto.Nodes is { Count: > 0 })
        {
            leafErrors.Add(scope.Node(label, $"{kind} 节点不能声明子节点，子节点只属于容器。"));
        }

        if (dto.In is { Count: > 0 })
        {
            leafErrors.Add(scope.Node(label,
                "执行节点不写输入端口绑定，接线一律经 From。"));
        }

        ErrorOr<IReadOnlyList<InputPort>> leafInputs = ToInputs(dto.Inputs, scope, label);
        if (leafInputs.IsError)
        {
            leafErrors.AddRange(leafInputs.ErrorsOrEmptyList);
        }

        if (HasModels(dto.Models))
        {
            leafErrors.Add(scope.Node(label, "执行节点不声明模型槽位与模型绑定，槽位声明只属于容器定义。"));
        }

        INodeType leafType = ReadParameters(declaration, dto, new NodeLabel(label), scope, leafErrors);

        OutputValidation? declaredValidation = null;
        if (dto.Validate is { } leafValidate)
        {
            ErrorOr<ValidationPredicate> parsedPredicate = ParseValidationPredicate(leafValidate.Predicate);
            if (parsedPredicate.IsError)
            {
                leafErrors.Add(scope.Node(label, parsedPredicate.FirstError.Description));
            }
            else
            {
                declaredValidation = new OutputValidation(parsedPredicate.Value, leafValidate.Argument);
            }
        }

        ErrorOr<IReadOnlyList<Port>> outputs = ToPorts(dto.Outputs, scope, label, allowValue: false);
        if (outputs.IsError)
        {
            leafErrors.AddRange(outputs.ErrorsOrEmptyList);
        }

        if (declared.DerivesOutputsFromInputs && dto.Outputs is { Count: > 0 })
        {
            leafErrors.Add(scope.Node(label,
                $"{declaration.Kind.Value}节点不声明输出端口，对外端口取自身输入端口。"));
        }

        if (leafErrors.Count > 0)
        {
            return leafErrors;
        }

        ErrorOr<IReadOnlyList<FromGroup>> parsedFrom = ToFrom(dto.From, scope, label);
        if (parsedFrom.IsError)
        {
            return parsedFrom.ErrorsOrEmptyList;
        }

        return NodeSpec.Define(new NodeName(dto.Name ?? string.Empty)) with
        {
            Type = leafType,
            Inputs = leafInputs.Value,
            From = parsedFrom.Value,
            Model = ToModel(dto.Model),
            Outputs = declared.DerivesOutputsFromInputs ? ForwardedPorts(leafInputs.Value) : outputs.Value,
            Spec = new ExecutableSpec
            {
                Validate = declaredValidation,
                MaxExecutions = dto.MaxExecutions,
            },
        };
    }

    /// <summary>派生对外端口：输入端口原样转发为同名同类型输出。</summary>
    private static IReadOnlyList<Port> ForwardedPorts(IReadOnlyList<InputPort> inputs) =>
    [
        .. inputs.Select(input => new Port(input.Name, input.Type, input.Description)),
    ];

    /// <summary>读出类型参数段并建出类型实例：未知字段与类型语义不合法的声明随错误一起丢弃。</summary>
    private static INodeType ReadParameters(
        INodeTypeFile declaration,
        NodeDto dto,
        NodeLabel label,
        NodeScope scope,
        List<Error> errors) =>
        NodeParameterJson.Read(declaration, scope, label, dto.Parameters, errors.Add);

    /// <summary>解析容器输入输出绑定表：键是端口名，值是端口引用，形状一致走同一个装配。</summary>
    private static ErrorOr<Dictionary<PortName, PortRef>?> ToBindings(Dictionary<string, string>? bindings)
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

    /// <summary>解析端口引用语法：@端口 是绑定引用，来源@端口 指向来源节点输出端口，
    /// 保留的拆分与上下文端口与声明端口同形。不带 @ 的裸名不是合法引用。</summary>
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

            NodeLabel source = new(text[..at]);
            return PortRef.Named(source, port.Value);
        }

        return Error.Validation(FlowErrorCodes.FlowNode,
            $"端口引用 {text} 缺少 @，必须写 来源@端口 或 @端口。");
    }

    /// <summary>解析输出端口表：条目写 Name 与 Type，Type 必填，说明可写 Description。
    /// allowValue 为真时接受开始端口的固定值 Value。</summary>
    private static ErrorOr<IReadOnlyList<Port>> ToPorts(List<JsonElement>? entries, NodeScope scope, string nodeLabel, bool allowValue)
    {
        if (entries is not { Count: > 0 })
        {
            return Array.Empty<Port>();
        }

        List<Port> result = [];
        List<Error> errors = [];
        foreach (JsonElement entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                errors.Add(scope.Node(nodeLabel, "输出端口条目必须是带 Name 与 Type 的对象，说明写 Description。"));
                continue;
            }

            string? declaredName = null;
            PortType? declaredType = null;
            string? declaredDescription = null;
            string? declaredValue = null;
            bool invalid = false;
            foreach (JsonProperty property in entry.EnumerateObject())
            {
                switch (property.Name.ToLowerInvariant())
                {
                    case "name":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeLabel, "输出端口的 Name 键必须是端口名。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredName = property.Value.GetString();
                        }
                        break;

                    case "type":
                        if (property.Value.ValueKind != JsonValueKind.String
                            || !Enum.TryParse(property.Value.GetString(), ignoreCase: true, out PortType parsedType)
                            || parsedType is not (PortType.Text or PortType.Context or PortType.Signal))
                        {
                            errors.Add(scope.Node(nodeLabel, "输出端口的 Type 应为 Text、Context 或 Signal。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredType = parsedType;
                        }
                        break;

                    case "description":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeLabel, "输出端口的 Description 键必须是说明文本。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredDescription = property.Value.GetString();
                        }
                        break;

                    case "ref":
                        errors.Add(scope.Node(nodeLabel, "执行节点的输出端口条目写 Name 与 Type，不写 Ref。"));
                        invalid = true;
                        break;

                    case "value" when allowValue:
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeLabel, "开始端口的 Value 键必须是文本。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredValue = property.Value.GetString();
                        }
                        break;

                    case "value":
                        errors.Add(scope.Node(nodeLabel, "输出端口不认识键 Value，Value 只属于开始端口。"));
                        invalid = true;
                        break;

                    default:
                        errors.Add(scope.Node(nodeLabel, allowValue
                            ? $"输出端口不认识键 {property.Name}，条目写 Name、Type、Description 与 Value。"
                            : $"输出端口不认识键 {property.Name}，条目写 Name、Type 与 Description。"));
                        invalid = true;
                        break;
                }
            }

            if (invalid)
            {
                continue;
            }

            ErrorOr<PortName> parsedName = PortName.Create(declaredName ?? string.Empty);
            if (parsedName.IsError)
            {
                errors.AddRange(parsedName.ErrorsOrEmptyList);
                continue;
            }

            if (declaredType is null)
            {
                errors.Add(scope.Node(nodeLabel, "输出端口的 Type 不能省略，必须是 Text、Context 或 Signal。"));
                continue;
            }

            result.Add(new Port(parsedName.Value, declaredType.Value, ToDescription(declaredDescription), declaredValue));
        }

        return errors.Count > 0 ? errors : result;
    }

    /// <summary>解析输入端口表：条目写 Name 与可选 Type、Description、Default，成员与执行节点都用它声明目标侧输入。</summary>
    private static ErrorOr<IReadOnlyList<InputPort>> ToInputs(
        List<JsonElement>? entries,
        NodeScope scope,
        string nodeLabel)
    {
        if (entries is not { Count: > 0 })
        {
            return Array.Empty<InputPort>();
        }

        List<InputPort> result = [];
        List<Error> errors = [];
        foreach (JsonElement entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                errors.Add(scope.Node(nodeLabel, "输入端口条目必须是带 Name 的对象，类型写 Type，说明写 Description，默认值写 Default。"));
                continue;
            }

            string? declaredName = null;
            PortType? declaredType = null;
            string? declaredDescription = null;
            string? declaredDefault = null;
            bool invalid = false;
            foreach (JsonProperty property in entry.EnumerateObject())
            {
                switch (property.Name.ToLowerInvariant())
                {
                    case "name":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeLabel, "输入端口的 Name 键必须是端口名。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredName = property.Value.GetString();
                        }
                        break;

                    case "type":
                        if (property.Value.ValueKind != JsonValueKind.String
                            || !Enum.TryParse(property.Value.GetString(), ignoreCase: true, out PortType parsedType)
                            || parsedType is not (PortType.Text or PortType.Context or PortType.Signal))
                        {
                            errors.Add(scope.Node(nodeLabel, "输入端口的 Type 应为 Text、Context 或 Signal。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredType = parsedType;
                        }
                        break;

                    case "description":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeLabel, "输入端口的 Description 键必须是说明文本。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredDescription = property.Value.GetString();
                        }
                        break;

                    case "default":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeLabel, "输入端口的 Default 键必须是文本。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredDefault = property.Value.GetString();
                        }
                        break;

                    default:
                        errors.Add(scope.Node(nodeLabel, $"输入端口不认识键 {property.Name}，条目写 Name、Type、Description 与 Default。"));
                        invalid = true;
                        break;
                }
            }

            if (invalid)
            {
                continue;
            }

            ErrorOr<PortName> parsedName = PortName.Create(declaredName ?? string.Empty);
            if (parsedName.IsError)
            {
                errors.AddRange(parsedName.ErrorsOrEmptyList);
                continue;
            }

            result.Add(new InputPort(
                parsedName.Value,
                declaredType ?? PortType.Text,
                ToDescription(declaredDescription),
                declaredDefault));
        }

        return errors.Count > 0 ? errors : result;
    }

    /// <summary>说明文本：未写或全空白时视为未写。</summary>
    private static string? ToDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    /// <summary>解析容器输出端口表：条目写 Name 与 Ref，Ref 写 成员@端口，端口名在同一容器内唯一，说明可写 Description。</summary>
    private static ErrorOr<Dictionary<PortName, ContainerOutput>?> ToContainerOutputs(
        List<JsonElement>? entries,
        NodeScope scope,
        string nodeLabel)
    {
        if (entries is not { Count: > 0 })
        {
            return (Dictionary<PortName, ContainerOutput>?)null;
        }

        Dictionary<PortName, ContainerOutput> result = [];
        List<Error> errors = [];
        foreach (JsonElement entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                errors.Add(scope.Node(nodeLabel, "容器输出端口条目必须是带 Name 与 Ref 的对象，说明写 Description。"));
                continue;
            }

            string? declaredName = null;
            string? declaredRef = null;
            string? declaredDescription = null;
            bool invalid = false;
            foreach (JsonProperty property in entry.EnumerateObject())
            {
                switch (property.Name.ToLowerInvariant())
                {
                    case "name":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeLabel, "容器输出端口的 Name 键必须是端口名。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredName = property.Value.GetString();
                        }
                        break;

                    case "ref":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeLabel, "容器输出端口的 Ref 键必须是 成员@端口 引用。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredRef = property.Value.GetString();
                        }
                        break;

                    case "description":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeLabel, "容器输出端口的 Description 键必须是说明文本。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredDescription = property.Value.GetString();
                        }
                        break;

                    default:
                        errors.Add(scope.Node(nodeLabel, $"容器输出端口不认识键 {property.Name}，条目写 Name、Ref 与 Description。"));
                        invalid = true;
                        break;
                }
            }

            if (invalid)
            {
                continue;
            }

            ErrorOr<PortName> parsedName = PortName.Create(declaredName ?? string.Empty);
            if (parsedName.IsError)
            {
                errors.AddRange(parsedName.ErrorsOrEmptyList);
                continue;
            }

            if (declaredRef is null)
            {
                errors.Add(scope.Node(nodeLabel, "容器输出端口的 Ref 不能省略，写 成员@端口。"));
                continue;
            }

            ErrorOr<PortRef> parsedRef = ParsePortRef(declaredRef);
            if (parsedRef.IsError)
            {
                errors.AddRange(parsedRef.ErrorsOrEmptyList);
                continue;
            }

            if (!result.TryAdd(parsedName.Value, new ContainerOutput(parsedRef.Value, ToDescription(declaredDescription))))
            {
                errors.Add(scope.Node(nodeLabel, $"容器输出端口 {parsedName.Value.Value} 重复。"));
            }
        }

        return errors.Count > 0 ? errors : result;
    }

    private static ModelRef? ToModel(string? model) =>
        string.IsNullOrWhiteSpace(model) ? null : new ModelRef(model);

    /// <summary>是否写了 Models 键。缺键与 JSON null 都读成空，只有带值的元素才算写了。</summary>
    private static bool HasModels(JsonElement? models) =>
        models is { ValueKind: not JsonValueKind.Undefined };

    /// <summary>容器定义的模型槽位声明：字符串数组，名字不能为空且不重复。</summary>
    private static ErrorOr<IReadOnlyList<ModelRef>> ToModelSlots(JsonElement models, NodeScope scope, string nodeLabel)
    {
        List<ModelRef> slots = [];
        List<Error> errors = [];
        foreach (JsonElement entry in models.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(entry.GetString()))
            {
                errors.Add(scope.Node(nodeLabel, "模型槽位名不能为空。"));
                continue;
            }

            slots.Add(new ModelRef(entry.GetString()!));
        }

        foreach (ModelRef duplicated in slots.GroupBy(slot => slot).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            errors.Add(scope.Node(nodeLabel, $"模型槽位 {duplicated} 重复。"));
        }

        return errors.Count > 0 ? errors : slots;
    }

    /// <summary>引用节点组的模型绑定：对象形状，键是槽位名，值是流程模型选择名。</summary>
    private static ErrorOr<Dictionary<ModelRef, ModelRef>?> ToModelBindings(JsonElement? models, NodeScope scope, string nodeLabel)
    {
        if (!HasModels(models))
        {
            return (Dictionary<ModelRef, ModelRef>?)null;
        }

        if (models!.Value.ValueKind != JsonValueKind.Object)
        {
            return scope.Node(nodeLabel, "引用节点组的 Models 写槽位绑定对象，槽位声明只写容器定义。");
        }

        Dictionary<ModelRef, ModelRef> bindings = [];
        List<Error> errors = [];
        foreach (JsonProperty property in models.Value.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name)
                || property.Value.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(property.Value.GetString()))
            {
                errors.Add(scope.Node(nodeLabel, "模型绑定键与值都不能为空。"));
                continue;
            }

            bindings[new ModelRef(property.Name)] = new ModelRef(property.Value.GetString()!);
        }

        return errors.Count > 0 ? errors : bindings;
    }

    /// <summary>把上游启动组装配成模型：数组元素是一个组，组内是输入端口名到来源端口引用的映射。
    /// 值必须是引用文本，字段类型不符记入错误，不抛异常。</summary>
    private static ErrorOr<IReadOnlyList<FromGroup>> ToFrom(List<JsonElement>? entries, NodeScope scope, string nodeLabel)
    {
        if (entries is not { Count: > 0 })
        {
            return Array.Empty<FromGroup>();
        }

        List<FromGroup> result = [];
        List<Error> errors = [];
        foreach (JsonElement entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                errors.Add(scope.Node(nodeLabel, "From 的每个组必须是对象，键写输入端口名，值写来源端口引用。"));
                continue;
            }

            List<FromLink> links = [];
            foreach (JsonProperty property in entry.EnumerateObject())
            {
                ErrorOr<PortName> input = PortName.Create(property.Name);
                if (input.IsError)
                {
                    errors.AddRange(input.ErrorsOrEmptyList);
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    errors.Add(scope.Node(nodeLabel, $"From 连接 {property.Name} 的来源必须是引用文本，写 @端口 或 来源@端口。"));
                    continue;
                }

                ErrorOr<PortRef> reference = ParsePortRef(property.Value.GetString() ?? string.Empty);
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

    /// <summary>把谓词名按名字匹配枚举，只接受白名单名字，数字字面量在装配时拒绝。</summary>
    private static ErrorOr<ValidationPredicate> ParseValidationPredicate(string? predicate)
    {
        if (string.IsNullOrWhiteSpace(predicate))
        {
            return Error.Validation(FlowErrorCodes.FlowNode, "Validate.Predicate 不能为空。");
        }

        string? matched = Enum.GetNames<ValidationPredicate>()
            .FirstOrDefault(name => string.Equals(name, predicate, StringComparison.OrdinalIgnoreCase));

        return matched is not null
            ? Enum.Parse<Kuroe.Flow.Shared.StaticGraph.Node.ValidationPredicate>(matched)
            : Error.Validation(FlowErrorCodes.FlowNode,
                $"Validate.Predicate 应为 NonEmpty、TextContains、TextNot、TextEquals 或 Pattern，收到 {predicate}。");
    }
}
