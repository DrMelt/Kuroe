using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Json.Files;

/// <summary>文件形状到描述文档的绑定：只判断格式与字段归属，不构造模型。</summary>
static class FlowDocumentReader
{
    /// <summary>把文件形状转成描述文档，错误逐条收集。</summary>
    public static ErrorOr<FlowDocument> Read(FlowFileDto file)
    {
        List<Error> errors = [];
        List<NodeDefinitionDocument> nodes = [];
        foreach (NodeDto dto in file.Nodes)
        {
            List<Error> nodeErrors = [];
            ErrorOr<NodeDefinitionDocument> node = ReadDefinition(dto, NodeScope.Library, nodeErrors);
            if (node.IsSuccess && nodeErrors.Count == 0)
            {
                nodes.Add(node.Value);
            }
            else
            {
                errors.AddRange(nodeErrors);
            }
        }

        List<FlowDocumentFlow> flows = [];
        foreach (FlowDto dto in file.Flows)
        {
            List<Error> flowErrors = [];
            ErrorOr<FlowDocumentFlow> flow = ReadFlow(dto, flowErrors);
            if (flow.IsSuccess && flowErrors.Count == 0)
            {
                flows.Add(flow.Value);
            }
            else
            {
                errors.AddRange(flowErrors);
            }
        }

        return errors.Count > 0 ? errors : new FlowDocument(nodes, flows);
    }

    private static ErrorOr<FlowDocumentFlow> ReadFlow(FlowDto dto, List<Error> errors)
    {
        string name = dto.Name?.Trim() ?? string.Empty;
        NodeScope scope = NodeScope.Flow(name);
        List<ModelDocument> models =
        [
            .. dto.Models.Select(model => new ModelDocument(model.Name ?? string.Empty, model.Model, model.Runtime ?? false)),
        ];

        ErrorOr<IReadOnlyList<PortDocument>> startOutputs = ReadPorts(dto.Start?.Outputs, scope, name, allowValue: true);
        if (startOutputs.IsError)
        {
            return startOutputs.ErrorsOrEmptyList;
        }

        Dictionary<string, string> hostStartInputs = [];
        foreach ((string port, string? input) in dto.HostStartInputs ?? [])
        {
            if (string.IsNullOrWhiteSpace(port) || string.IsNullOrWhiteSpace(input))
            {
                errors.Add(FlowErrors.Body(name, "宿主开始输入的端口名与输入名都不能为空。"));
                continue;
            }

            hostStartInputs[port.Trim()] = input.Trim();
        }

        NodeDocument? root = null;
        if (dto.Nodes.Count > 1)
        {
            errors.Add(FlowErrors.Body(name, "流程只能有一个根节点。"));
        }
        else if (dto.Nodes.Count == 1)
        {
            ErrorOr<NodeDocument> parsed = ReadNode(dto.Nodes[0], NodeScope.Flow(name), errors);
            if (parsed.IsSuccess)
            {
                root = parsed.Value;
            }
        }

        return errors.Count > 0
            ? errors
            : new FlowDocumentFlow(name, dto.Description, models, startOutputs.Value, hostStartInputs, root);
    }

    private static ErrorOr<NodeDocument> ReadNode(NodeDto dto, NodeScope scope, List<Error> errors)
    {
        if (dto.Use is { Length: > 0 })
        {
            ErrorOr<NodeReferenceDocument> reference = ReadReference(dto, scope, errors);

            return reference.IsError ? reference.ErrorsOrEmptyList : reference.Value;
        }

        ErrorOr<NodeDefinitionDocument> definition = ReadDefinition(dto, scope, errors);

        return definition.IsError ? definition.ErrorsOrEmptyList : definition.Value;
    }

    private static ErrorOr<NodeDefinitionDocument> ReadDefinition(NodeDto dto, NodeScope scope, List<Error> errors)
    {
        string label = dto.Name ?? string.Empty;
        List<Error> nodeErrors = [];
        if (dto.Use is { Length: > 0 })
        {
            nodeErrors.Add(scope.Node(label, "节点库定义不能是引用。"));
            errors.AddRange(nodeErrors);

            return nodeErrors;
        }

        if (dto.Label is not null)
        {
            nodeErrors.Add(scope.Node(dto.Label, "定义条目写节点名 Name，Label 只属于被使用的节点。"));
            errors.AddRange(nodeErrors);

            return nodeErrors;
        }

        ErrorOr<IReadOnlyList<InputPortDocument>> inputs = ReadInputs(dto.Inputs, scope, label);
        if (inputs.IsError)
        {
            nodeErrors.AddRange(inputs.ErrorsOrEmptyList);
        }

        ErrorOr<IReadOnlyList<PortDocument>> outputs = ReadPorts(dto.Outputs, scope, label, allowValue: false);
        if (outputs.IsError)
        {
            nodeErrors.AddRange(outputs.ErrorsOrEmptyList);
        }

        bool hasModelBindings = dto.Models is { ValueKind: JsonValueKind.Object };
        ErrorOr<List<string>?> slots = hasModelBindings
            ? (List<string>?)null
            : ReadModelSlots(dto.Models, scope, label);
        if (slots.IsError)
        {
            nodeErrors.AddRange(slots.ErrorsOrEmptyList);
        }

        ErrorOr<IReadOnlyList<FromGroupDocument>> from = ReadFrom(dto.From, scope, label);
        if (from.IsError)
        {
            nodeErrors.AddRange(from.ErrorsOrEmptyList);
        }

        List<NodeDocument>? children = null;
        if (dto.Nodes is { Count: > 0 } childDtos)
        {
            children = [];
            foreach (NodeDto child in childDtos)
            {
                ErrorOr<NodeDocument> parsed = ReadNode(child, scope, nodeErrors);
                if (parsed.IsSuccess)
                {
                    children.Add(parsed.Value);
                }
            }
        }

        if (nodeErrors.Count > 0)
        {
            errors.AddRange(nodeErrors);

            return nodeErrors;
        }

        return new NodeDefinitionDocument
        {
            Name = label,
            Kind = dto.Kind ?? string.Empty,
            Inputs = inputs.Value,
            Outputs = outputs.Value,
            ModelSlots = slots.Value,
            Model = dto.Model,
            HasModelBindings = hasModelBindings,
            In = dto.In,
            Nodes = children,
            Parameters = ReadParameters(dto.Parameters),
            From = from.Value,
            Validate = ReadValidation(dto.Validate),
            MaxExecutions = dto.MaxExecutions,
        };
    }

    private static ErrorOr<NodeReferenceDocument> ReadReference(NodeDto dto, NodeScope scope, List<Error> errors)
    {
        string label = dto.Label ?? string.Empty;
        List<Error> referenceErrors = [];
        if (dto.Name is not null)
        {
            referenceErrors.Add(scope.Node(dto.Name, "被使用的节点写 Label 区分，不写 Name。"));
            errors.AddRange(referenceErrors);

            return referenceErrors;
        }

        if (string.IsNullOrWhiteSpace(dto.Label))
        {
            referenceErrors.Add(scope.Node(dto.Use ?? string.Empty, "被使用的节点必须写 Label，它是节点在图里的标识段。"));
            errors.AddRange(referenceErrors);

            return referenceErrors;
        }

        if (dto.Kind is not null)
        {
            referenceErrors.Add(scope.Node(label, "引用节点的类型由被引用的库定义决定，不写 Kind。"));
        }

        if (dto.Nodes is { Count: > 0 })
        {
            referenceErrors.Add(scope.Node(label, "引用成员不能同时声明子节点。"));
        }

        if (dto.Parameters is { Count: > 0 } parameters)
        {
            referenceErrors.Add(scope.Node(label, $"引用节点不能声明类型参数 {string.Join("、", parameters.Keys)}。"));
        }

        if (dto.Inputs is { Count: > 0 })
        {
            referenceErrors.Add(scope.Node(label, "引用节点不能声明输入端口，端口声明属于被引用的定义。"));
        }

        if (dto.Outputs is { Count: > 0 })
        {
            referenceErrors.Add(scope.Node(label, "引用节点不能声明输出端口。"));
        }

        if (dto.Model is not null && HasModels(dto.Models))
        {
            referenceErrors.Add(scope.Node(label, "引用节点不能同时声明模型与模型绑定。"));
        }

        ErrorOr<Dictionary<string, string>?> models = ReadModelBindings(dto.Models, scope, label);
        if (models.IsError)
        {
            referenceErrors.AddRange(models.ErrorsOrEmptyList);
        }

        ErrorOr<IReadOnlyList<FromGroupDocument>> from = ReadFrom(dto.From, scope, label);
        if (from.IsError)
        {
            referenceErrors.AddRange(from.ErrorsOrEmptyList);
        }

        if (referenceErrors.Count > 0)
        {
            errors.AddRange(referenceErrors);

            return referenceErrors;
        }

        return new NodeReferenceDocument
        {
            Label = label,
            Use = dto.Use ?? string.Empty,
            In = dto.In,
            Model = dto.Model,
            Models = models.Value,
            From = from.Value,
            Validate = ReadValidation(dto.Validate),
            MaxExecutions = dto.MaxExecutions,
        };
    }

    private static ErrorOr<IReadOnlyList<PortDocument>> ReadPorts(
        List<JsonElement>? entries,
        NodeScope scope,
        string label,
        bool allowValue)
    {
        if (entries is not { Count: > 0 })
        {
            return Array.Empty<PortDocument>();
        }

        List<PortDocument> result = [];
        List<Error> errors = [];
        foreach (JsonElement entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                errors.Add(scope.Node(label, "输出端口条目必须是带 Name 与 Type 的对象，说明写 Description。"));
                continue;
            }

            string? name = null;
            PortType? type = null;
            string? description = null;
            string? value = null;
            string? reference = null;
            bool invalid = false;
            foreach (JsonProperty property in entry.EnumerateObject())
            {
                switch (property.Name.ToLowerInvariant())
                {
                    case "name":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(label, "输出端口的 Name 键必须是端口名。"));
                            invalid = true;
                        }
                        else
                        {
                            name = property.Value.GetString();
                        }

                        break;

                    case "type":
                        if (!TryPortType(property.Value, out PortType parsedType))
                        {
                            errors.Add(scope.Node(label, "输出端口的 Type 应为已注册的端口类型名。"));
                            invalid = true;
                        }
                        else
                        {
                            type = parsedType;
                        }

                        break;

                    case "description":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(label, "输出端口的 Description 键必须是说明文本。"));
                            invalid = true;
                        }
                        else
                        {
                            description = property.Value.GetString();
                        }

                        break;

                    case "ref":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(label, "容器输出端口的 Ref 键必须是 成员@端口 引用。"));
                            invalid = true;
                        }
                        else
                        {
                            reference = property.Value.GetString();
                        }

                        break;

                    case "value" when allowValue:
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(label, "开始端口的 Value 键必须是文本。"));
                            invalid = true;
                        }
                        else
                        {
                            value = property.Value.GetString();
                        }

                        break;

                    case "value":
                        errors.Add(scope.Node(label, "输出端口不认识键 Value，Value 只属于开始端口。"));
                        invalid = true;
                        break;

                    default:
                        errors.Add(scope.Node(label, allowValue
                            ? $"输出端口不认识键 {property.Name}，条目写 Name、Type、Description 与 Value。"
                            : $"输出端口不认识键 {property.Name}，条目写 Name、Type、Description 与 Ref。"));
                        invalid = true;
                        break;
                }
            }

            if (!invalid)
            {
                result.Add(new PortDocument(name ?? string.Empty, type, description, value, reference));
            }
        }

        return errors.Count > 0 ? errors : result;
    }

    private static ErrorOr<IReadOnlyList<InputPortDocument>> ReadInputs(
        List<JsonElement>? entries,
        NodeScope scope,
        string label)
    {
        if (entries is not { Count: > 0 })
        {
            return Array.Empty<InputPortDocument>();
        }

        List<InputPortDocument> result = [];
        List<Error> errors = [];
        foreach (JsonElement entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                errors.Add(scope.Node(label, "输入端口条目必须是带 Name 的对象，类型写 Type，说明写 Description，默认值写 Default。"));
                continue;
            }

            string? name = null;
            PortType? type = null;
            string? description = null;
            string? defaultValue = null;
            bool invalid = false;
            foreach (JsonProperty property in entry.EnumerateObject())
            {
                switch (property.Name.ToLowerInvariant())
                {
                    case "name":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(label, "输入端口的 Name 键必须是端口名。"));
                            invalid = true;
                        }
                        else
                        {
                            name = property.Value.GetString();
                        }

                        break;

                    case "type":
                        if (!TryPortType(property.Value, out PortType parsedType))
                        {
                            errors.Add(scope.Node(label, "输入端口的 Type 应为已注册的端口类型名。"));
                            invalid = true;
                        }
                        else
                        {
                            type = parsedType;
                        }

                        break;

                    case "description":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(label, "输入端口的 Description 键必须是说明文本。"));
                            invalid = true;
                        }
                        else
                        {
                            description = property.Value.GetString();
                        }

                        break;

                    case "default":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(label, "输入端口的 Default 键必须是文本。"));
                            invalid = true;
                        }
                        else
                        {
                            defaultValue = property.Value.GetString();
                        }

                        break;

                    default:
                        errors.Add(scope.Node(label, $"输入端口不认识键 {property.Name}，条目写 Name、Type、Description 与 Default。"));
                        invalid = true;
                        break;
                }
            }

            if (!invalid)
            {
                result.Add(new InputPortDocument(name ?? string.Empty, type, description, defaultValue));
            }
        }

        return errors.Count > 0 ? errors : result;
    }

    private static bool TryPortType(JsonElement element, out PortType type)
    {
        type = default;

        return element.ValueKind == JsonValueKind.String
            && PortTypes.TryParse(element.GetString(), out type);
    }

    private static ErrorOr<List<string>?> ReadModelSlots(JsonElement? models, NodeScope scope, string label)
    {
        if (!HasModels(models))
        {
            return (List<string>?)null;
        }

        if (models!.Value.ValueKind != JsonValueKind.Array)
        {
            return scope.Node(label, "定义的 Models 写槽位名数组，模型绑定只属于引用节点组。");
        }

        List<string> slots = [];
        List<Error> errors = [];
        foreach (JsonElement entry in models.Value.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(entry.GetString()))
            {
                errors.Add(scope.Node(label, "模型槽位名不能为空。"));
                continue;
            }

            slots.Add(entry.GetString()!);
        }

        return errors.Count > 0 ? errors : slots;
    }

    private static ErrorOr<Dictionary<string, string>?> ReadModelBindings(
        JsonElement? models,
        NodeScope scope,
        string label)
    {
        if (!HasModels(models))
        {
            return (Dictionary<string, string>?)null;
        }

        if (models!.Value.ValueKind != JsonValueKind.Object)
        {
            return scope.Node(label, "引用节点组的 Models 写槽位绑定对象，槽位声明只写容器定义。");
        }

        Dictionary<string, string> bindings = [];
        List<Error> errors = [];
        foreach (JsonProperty property in models.Value.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name)
                || property.Value.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(property.Value.GetString()))
            {
                errors.Add(scope.Node(label, "模型绑定键与值都不能为空。"));
                continue;
            }

            bindings[property.Name] = property.Value.GetString()!;
        }

        return errors.Count > 0 ? errors : bindings;
    }

    private static ErrorOr<IReadOnlyList<FromGroupDocument>> ReadFrom(
        List<JsonElement>? entries,
        NodeScope scope,
        string label)
    {
        if (entries is not { Count: > 0 })
        {
            return Array.Empty<FromGroupDocument>();
        }

        List<FromGroupDocument> result = [];
        List<Error> errors = [];
        foreach (JsonElement entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                errors.Add(scope.Node(label, "From 的每个组必须是对象，键写输入端口名，值写来源端口引用。"));
                continue;
            }

            List<FromLinkDocument> links = [];
            foreach (JsonProperty property in entry.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    errors.Add(scope.Node(label, $"From 连接 {property.Name} 的来源必须是引用文本，写 @端口 或 来源@端口。"));
                    continue;
                }

                links.Add(new FromLinkDocument(property.Name, property.Value.GetString() ?? string.Empty));
            }

            if (links.Count == 0)
            {
                errors.Add(scope.Node(label, "From 的组至少要有一条连接。"));
                continue;
            }

            result.Add(new FromGroupDocument(links));
        }

        return errors.Count > 0 ? errors : result;
    }

    private static ValidationDocument? ReadValidation(ValidationDto? validation) =>
        validation is null ? null : new ValidationDocument(validation.Predicate ?? string.Empty, validation.Argument);

    private static bool HasModels(JsonElement? models) =>
        models is { ValueKind: not JsonValueKind.Undefined };

    private static Dictionary<string, FlowValue> ReadParameters(
        IReadOnlyDictionary<string, JsonElement>? parameters) =>
        parameters is not { Count: > 0 }
            ? []
            : parameters.ToDictionary(entry => entry.Key, entry => ToFlowValue(entry.Value));

    private static FlowValue ToFlowValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => new FlowValue.Text(element.GetString()!),
        JsonValueKind.Number => new FlowValue.Number(element.GetRawText()),
        JsonValueKind.True => new FlowValue.Boolean(true),
        JsonValueKind.False => new FlowValue.Boolean(false),
        JsonValueKind.Null or JsonValueKind.Undefined => new FlowValue.Null(),
        JsonValueKind.Array => new FlowValue.Array([.. element.EnumerateArray().Select(ToFlowValue)]),
        JsonValueKind.Object => new FlowValue.Object(
            element.EnumerateObject().ToDictionary(property => property.Name, property => ToFlowValue(property.Value))),
        _ => new FlowValue.Null(),
    };
}