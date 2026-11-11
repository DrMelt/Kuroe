using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Json.Files;

/// <summary>文件形状到描述文档的绑定：只判断格式与字段归属，不构造模型。
/// 文件层只认结构字段与参数段的对象形状，参数段里的键与取值由节点类型解释。</summary>
static class FlowFileReader
{
    /// <summary>把节点库文件形状转成节点定义，错误逐条收集。</summary>
    public static ErrorOr<IReadOnlyList<NodeDefinitionDocument>> ReadNodes(NodeFileDto file)
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

        return errors.Count > 0 ? errors : nodes;
    }

    /// <summary>把流程文件形状转成流程描述，错误逐条收集。</summary>
    public static ErrorOr<IReadOnlyList<FlowDocumentFlow>> ReadFlows(FlowFileDto file)
    {
        List<Error> errors = [];
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

        return errors.Count > 0 ? errors : flows;
    }

    private static ErrorOr<FlowDocumentFlow> ReadFlow(FlowDto dto, List<Error> errors)
    {
        string name = dto.Name?.Trim() ?? string.Empty;
        NodeScope scope = NodeScope.Flow(name);

        ErrorOr<IReadOnlyList<PortDocument>> ports = ReadPorts(dto.Ports, scope, name, allowDefault: true);
        if (ports.IsError)
        {
            errors.AddRange(ports.ErrorsOrEmptyList);
            return errors;
        }

        NodeDocument? root = null;
        if (dto.Root is { } rootNode)
        {
            ErrorOr<NodeDocument> parsed = ReadNode(rootNode, scope, errors);
            if (parsed.IsSuccess)
            {
                root = parsed.Value;
            }
        }

        return errors.Count > 0
            ? errors
            : new FlowDocumentFlow(name, dto.Description, ports.Value, root);
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

        ReportUnrecognized(dto, scope, label, nodeErrors);

        ErrorOr<IReadOnlyList<InputPortDocument>> inputs = ReadInputs(dto.Inputs, scope, label);
        if (inputs.IsError)
        {
            nodeErrors.AddRange(inputs.ErrorsOrEmptyList);
        }

        ErrorOr<IReadOnlyList<PortDocument>> outputs = ReadPorts(dto.Outputs, scope, label);
        if (outputs.IsError)
        {
            nodeErrors.AddRange(outputs.ErrorsOrEmptyList);
        }

        ErrorOr<Dictionary<string, string>?> bindings = ReadBindings(dto.Bindings, scope, label);
        if (bindings.IsError)
        {
            nodeErrors.AddRange(bindings.ErrorsOrEmptyList);
        }

        ErrorOr<IReadOnlyList<FromGroupDocument>> from = ReadFrom(dto.From, scope, label);
        if (from.IsError)
        {
            nodeErrors.AddRange(from.ErrorsOrEmptyList);
        }

        NodeEmission? emission = ReadEmission(dto, scope, label, nodeErrors);
        IReadOnlyDictionary<string, FlowValue>? parameters = ReadParameters(dto.Parameters, scope, label, nodeErrors);

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
            Bindings = bindings.Value,
            Nodes = children,
            Parameters = parameters is null ? NodeParameters.Empty : new NodeParameters(parameters),
            From = from.Value,
            Emission = emission,
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

        if (dto.Parameters is { } parameters && parameters.ValueKind != JsonValueKind.Null)
        {
            referenceErrors.Add(scope.Node(label, "引用节点的类型参数由被引用的库定义决定，不写 Parameters。"));
        }

        ReportUnrecognized(dto, scope, label, referenceErrors);

        if (dto.Inputs is { Count: > 0 })
        {
            referenceErrors.Add(scope.Node(label, "引用节点不能声明输入端口，端口声明属于被引用的定义。"));
        }

        if (dto.Outputs is { Count: > 0 })
        {
            referenceErrors.Add(scope.Node(label, "引用节点不能声明输出端口。"));
        }

        ErrorOr<Dictionary<string, string>?> bindings = ReadBindings(dto.Bindings, scope, label);
        if (bindings.IsError)
        {
            referenceErrors.AddRange(bindings.ErrorsOrEmptyList);
        }

        ErrorOr<IReadOnlyList<FromGroupDocument>> from = ReadFrom(dto.From, scope, label);
        if (from.IsError)
        {
            referenceErrors.AddRange(from.ErrorsOrEmptyList);
        }

        NodeEmission? emission = ReadEmission(dto, scope, label, referenceErrors);

        if (referenceErrors.Count > 0)
        {
            errors.AddRange(referenceErrors);

            return referenceErrors;
        }

        return new NodeReferenceDocument
        {
            Label = label,
            Use = dto.Use ?? string.Empty,
            Bindings = bindings.Value,
            From = from.Value,
            Emission = emission,
            MaxExecutions = dto.MaxExecutions,
        };
    }

    /// <summary>节点对象上不属于结构字段的键：类型专属字段只能写在参数段里，写在节点对象上是位置错误。</summary>
    private static void ReportUnrecognized(NodeDto dto, NodeScope scope, string label, List<Error> errors)
    {
        if (dto.Unrecognized is not { Count: > 0 } unrecognized)
        {
            return;
        }

        foreach (string key in unrecognized.Keys)
        {
            errors.Add(scope.Node(label, $"字段 {key} 不是节点结构字段，类型专属字段写在 Parameters 对象里。"));
        }
    }

    private static NodeEmission? ReadEmission(NodeDto dto, NodeScope scope, string label, List<Error> errors)
    {
        if (dto.Emission is not { Length: > 0 } text)
        {
            return null;
        }

        if (Enum.TryParse(text, ignoreCase: true, out NodeEmission emission))
        {
            return emission;
        }

        errors.Add(scope.Node(label, "节点的 Emission 应为 Repeated 或 Once。"));
        return null;
    }

    private static ErrorOr<IReadOnlyList<PortDocument>> ReadPorts(
        List<JsonElement>? entries,
        NodeScope scope,
        string label,
        bool allowDefault = false)
    {
        if (entries is not { Count: > 0 })
        {
            return Array.Empty<PortDocument>();
        }

        List<PortDocument> result = [];
        List<Error> errors = [];
        string kind = allowDefault ? "开始端口" : "输出端口";
        foreach (JsonElement entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                errors.Add(scope.Node(label, $"{kind}条目必须是带 Name 的对象，类型写 Type，说明写 Description。"));
                continue;
            }

            string? name = null;
            PortType? type = null;
            PortMode? mode = null;
            string? description = null;
            string? value = null;
            string? reference = null;
            string? defaultValue = null;
            bool invalid = false;
            foreach (JsonProperty property in entry.EnumerateObject())
            {
                switch (property.Name.ToLowerInvariant())
                {
                    case "name":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(label, $"{kind}的 Name 键必须是端口名。"));
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
                            errors.Add(scope.Node(label, $"{kind}的 Type 应为已注册的端口类型名。"));
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
                            errors.Add(scope.Node(label, $"{kind}的 Description 键必须是说明文本。"));
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
                            errors.Add(scope.Node(label, $"{kind}的 Ref 键必须是 来源@端口 或 @端口 引用。"));
                            invalid = true;
                        }
                        else
                        {
                            reference = property.Value.GetString();
                        }

                        break;

                    case "value":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(label, $"{kind}的 Value 键必须是文本。"));
                            invalid = true;
                        }
                        else
                        {
                            value = property.Value.GetString();
                        }

                        break;

                    case "mode":
                        if (!TryPortMode(property.Value, out PortMode parsedMode))
                        {
                            errors.Add(scope.Node(label, $"{kind}的 Mode 应为 Plain、Trigger 或 Constant。"));
                            invalid = true;
                        }
                        else
                        {
                            mode = parsedMode;
                        }

                        break;

                    case "default" when allowDefault:
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(label, $"{kind}的 Default 键必须是文本。"));
                            invalid = true;
                        }
                        else
                        {
                            defaultValue = property.Value.GetString();
                        }

                        break;

                    default:
                        string keys = allowDefault
                            ? "Name、Type、Mode、Description、Value、Ref 与 Default"
                            : "Name、Type、Mode、Description、Value 与 Ref";
                        errors.Add(scope.Node(label, $"{kind}不认识键 {property.Name}，条目写 {keys}。"));
                        invalid = true;
                        break;
                }
            }

            if (!invalid)
            {
                result.Add(new PortDocument(name ?? string.Empty, type, mode, description, value, reference, defaultValue));
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
            PortMode? mode = null;
            string? description = null;
            string? defaultValue = null;
            InputRole? role = null;
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

                    case "mode":
                        if (!TryPortMode(property.Value, out PortMode parsedMode))
                        {
                            errors.Add(scope.Node(label, "输入端口的 Mode 应为 Plain、Trigger 或 Constant。"));
                            invalid = true;
                        }
                        else
                        {
                            mode = parsedMode;
                        }

                        break;

                    case "role":
                        if (!TryInputRole(property.Value, out InputRole parsedRole))
                        {
                            errors.Add(scope.Node(label, "输入端口的 Role 应为 Content、Configuration 或 Control。"));
                            invalid = true;
                        }
                        else
                        {
                            role = parsedRole;
                        }

                        break;

                    default:
                        errors.Add(scope.Node(label,
                            $"输入端口不认识键 {property.Name}，条目写 Name、Type、Mode、Description、Default 与 Role。"));
                        invalid = true;
                        break;
                }
            }

            if (!invalid)
            {
                result.Add(new InputPortDocument(name ?? string.Empty, type, mode, description, defaultValue, role));
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

    private static bool TryPortMode(JsonElement element, out PortMode mode)
    {
        mode = default;

        return element.ValueKind == JsonValueKind.String
            && Enum.TryParse(element.GetString(), ignoreCase: true, out mode);
    }

    private static bool TryInputRole(JsonElement element, out InputRole role)
    {
        role = default;

        return element.ValueKind == JsonValueKind.String
            && Enum.TryParse(element.GetString(), ignoreCase: true, out role);
    }

    private static bool TryTriggerCondition(JsonElement element, out TriggerCondition condition)
    {
        condition = default;

        if (element.ValueKind != JsonValueKind.String || element.GetString() is not { } text)
        {
            return false;
        }

        return Enum.TryParse(text, ignoreCase: true, out condition)
            && Enum.IsDefined(condition)
            && text.Equals(condition.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static ErrorOr<Dictionary<string, string>?> ReadBindings(
        Dictionary<string, JsonElement>? entries,
        NodeScope scope,
        string label)
    {
        if (entries is not { Count: > 0 })
        {
            return (Dictionary<string, string>?)null;
        }

        Dictionary<string, string> bindings = [];
        List<Error> errors = [];
        foreach ((string name, JsonElement entry) in entries)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(scope.Node(label, "输入端口名不能为空。"));
                continue;
            }

            if (entry.ValueKind != JsonValueKind.String)
            {
                errors.Add(scope.Node(label, "输入绑定的值只写引用文本，Condition 只写在 From。"));
                continue;
            }

            string reference = entry.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reference))
            {
                errors.Add(scope.Node(label, "输入绑定的来源引用不能为空。"));
                continue;
            }

            bindings[name.Trim()] = reference.Trim();
        }

        return errors.Count > 0 ? errors : bindings;
    }

    /// <summary>读一条 From 来源：字符串即引用文本，对象写 Ref 与可选 Condition。</summary>
    private static ErrorOr<(string Ref, TriggerCondition? Condition)> ReadFromSource(
        JsonElement entry,
        NodeScope scope,
        string label)
    {
        if (entry.ValueKind == JsonValueKind.String)
        {
            return (entry.GetString() ?? string.Empty, null);
        }

        if (entry.ValueKind != JsonValueKind.Object)
        {
            return scope.Node(label, "From 连接的来源必须是引用文本，或带 Ref 与可选 Condition 的对象。");
        }

        string? reference = null;
        TriggerCondition? condition = null;
        foreach (JsonProperty property in entry.EnumerateObject())
        {
            switch (property.Name.ToLowerInvariant())
            {
                case "ref" when property.Value.ValueKind == JsonValueKind.String:
                    reference = property.Value.GetString();
                    break;

                case "ref":
                    return scope.Node(label, "From 连接的 Ref 键必须是 来源@端口 或 @端口 引用。");

                case "condition":
                    {
                        if (!TryTriggerCondition(property.Value, out TriggerCondition parsed))
                        {
                            return scope.Node(label, "From 连接的 Condition 应为 Positive 或 Negative。");
                        }

                        condition = parsed;
                        break;
                    }

                default:
                    return scope.Node(label, $"From 连接的来源对象不认识键 {property.Name}，只写 Ref 与 Condition。");
            }
        }

        if (reference is null)
        {
            return scope.Node(label, "From 连接的来源对象必须写 Ref 键。");
        }

        return (reference, condition);
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
                errors.Add(scope.Node(label, "From 的每个组必须是对象，键写输入端口名，值写来源。"));
                continue;
            }

            List<FromLinkDocument> links = [];
            foreach (JsonProperty property in entry.EnumerateObject())
            {
                ErrorOr<(string Ref, TriggerCondition? Condition)> source =
                    ReadFromSource(property.Value, scope, label);
                if (source.IsError)
                {
                    errors.AddRange(source.ErrorsOrEmptyList);
                    continue;
                }

                links.Add(new FromLinkDocument(property.Name, source.Value.Ref, source.Value.Condition));
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

    /// <summary>读参数段：文件层只要求它是对象且键非空，键与取值属于节点类型。</summary>
    private static Dictionary<string, FlowValue>? ReadParameters(
        JsonElement? parameters,
        NodeScope scope,
        string label,
        List<Error> errors)
    {
        if (parameters is not { } element || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(scope.Node(label, "Parameters 必须是对象，键是节点类型声明的字段名。"));

            return null;
        }

        Dictionary<string, FlowValue> values = [];
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name))
            {
                errors.Add(scope.Node(label, "Parameters 的字段名不能为空。"));
                continue;
            }

            values[property.Name] = ToFlowValue(property.Value);
        }

        return values;
    }

    private static FlowValue ToFlowValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => new FlowValue.Text(element.ToString()),
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