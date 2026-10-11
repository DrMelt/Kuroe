using System.Text.Json;
using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Nodes.Library;
using Kuroe.Shared;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Workflows.Flows.Files;
using Kuroe.Workflows.Flows.Model;
using ModelDefinition = Kuroe.Shared.Workflows.Flows.ModelDefinition;

namespace Kuroe.Workflows.Flows;

/// <summary>流程文件的装配：把文件形状的 DTO 绑成模型并校验、展开，或把模型转回文件形状。
/// 文件存取由 FlowStore 承担。</summary>
static class FlowAssembler
{
    /// <summary>从流程文件装配模板集合，文件不存在时用内置内容。</summary>
    public static ErrorOr<AssembledFlows> Load(FlowStore store, NodeLibrary library)
    {
        ErrorOr<FlowFileDto?> loaded = store.LoadDto();
        if (loaded.IsError)
        {
            return loaded.ErrorsOrEmptyList;
        }

        return loaded.Value is null
            ? Assemble([], DefaultFlows.Builtin, library)
            : Assemble(loaded.Value, library);
    }

    /// <summary>从指定文件读取节点定义与流程，供导入使用。</summary>
    public static ErrorOr<(IReadOnlyList<NodeSpec> Nodes, IReadOnlyList<FlowDefinition> Flows)> Read(string path)
    {
        ErrorOr<FlowFileDto> read = FlowStore.ReadDto(path);

        return read.IsError ? read.ErrorsOrEmptyList : ToContent(read.Value);
    }

    /// <summary>把节点定义与流程写回流程文件，只写文件来源的节点定义。</summary>
    public static ErrorOr<Success> Save(
        FlowStore store,
        IReadOnlyList<NodeSpec> fileNodes,
        IReadOnlyList<FlowDefinition> flows) =>
        store.Save(ToDto(fileNodes, flows));

    /// <summary>装配文件形状：DTO 转模型后校验并展开。</summary>
    public static ErrorOr<AssembledFlows> Assemble(FlowFileDto dto, NodeLibrary library)
    {
        ErrorOr<(IReadOnlyList<NodeSpec> Nodes, IReadOnlyList<FlowDefinition> Flows)> content = ToContent(dto);

        return content.IsError
            ? content.ErrorsOrEmptyList
            : Assemble(content.Value.Nodes, content.Value.Flows, library);
    }

    /// <summary>校验并展开模型内容：文件节点并入节点库副本后逐流程展开，错误一次给全，流程名重复一并报出。
    /// 文件内节点重名直接报错，结果里的节点库是叠加文件定义后的生效库。</summary>
    public static ErrorOr<AssembledFlows> Assemble(
        IReadOnlyList<NodeSpec> fileNodes,
        IReadOnlyList<FlowDefinition> flows,
        NodeLibrary library)
    {
        NodeLibrary effective = library.Copy();
        List<Error> errors = [.. effective.Register(fileNodes)
            .Select(name => NodeScope.Library.Node(name.Value, "节点名重复。"))];
        if (errors.Count > 0)
        {
            return errors;
        }

        errors.AddRange(Validate(effective, flows, out List<FlowDefinition> valid));
        if (FlowRules.Duplicated(valid) is { } repeated)
        {
            errors.Add(FlowErrors.Body(repeated.Value, "流程名重复。"));
        }

        return errors.Count > 0 ? errors : new AssembledFlows(effective, fileNodes, flows, valid);
    }

    /// <summary>校验节点库，合法时逐流程展开校验。节点库不合法时只给节点库错误，合法流程经 valid 交回。</summary>
    private static List<Error> Validate(
        NodeLibrary library,
        IReadOnlyList<FlowDefinition> flows,
        out List<FlowDefinition> valid)
    {
        ErrorOr<Success> libraryChecked = NodeLibraryRules.Validate(library);
        if (libraryChecked.IsError)
        {
            valid = [];

            return [.. libraryChecked.ErrorsOrEmptyList];
        }

        List<Error> errors = [];
        List<FlowDefinition> accepted = [];
        foreach (FlowDefinition flow in flows)
        {
            ErrorOr<NodeSpec> expanded = NodeExpander.Expand(library, flow.RootNode, NodeScope.Flow(flow.Name.Value));
            if (expanded.IsError)
            {
                errors.AddRange(expanded.ErrorsOrEmptyList);
                continue;
            }

            FlowDefinition concrete = new(flow.Name, flow.Description, flow.Models, flow.StartOutputs, expanded.Value);
            ErrorOr<Success> checkedFlow = FlowRules.Validate(concrete);
            if (checkedFlow.IsError)
            {
                errors.AddRange(checkedFlow.ErrorsOrEmptyList);
            }
            else
            {
                accepted.Add(concrete);
            }
        }

        valid = accepted;

        return errors;
    }

    /// <summary>把文件形状转成节点定义与流程模型，错误逐条收集。</summary>
    private static ErrorOr<(IReadOnlyList<NodeSpec> Nodes, IReadOnlyList<FlowDefinition> Flows)> ToContent(FlowFileDto file)
    {
        List<NodeSpec> nodes = [];
        List<Error> errors = [];
        foreach (NodeDto dto in file.Nodes)
        {
            ErrorOr<NodeSpec> converted = ToNode(dto, NodeScope.Library);
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
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errors.Add(FlowErrors.MissingName());

                continue;
            }

            ErrorOr<FlowDefinition> flow = ToFlowDefinition(dto);
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
    private static ErrorOr<FlowDefinition> ToFlowDefinition(FlowDto dto)
    {
        List<ModelDefinition> models = [];
        List<Error> modelErrors = [];
        foreach (ModelDto model in dto.Models)
        {
            ModelName? modelName = null;
            if (model.Model is { Length: > 0 } text && !string.IsNullOrWhiteSpace(text))
            {
                ErrorOr<ModelName> parsed = ModelName.Create(text.Trim());
                if (parsed.IsError)
                {
                    modelErrors.Add(FlowErrors.Model(dto.Name ?? string.Empty, model.Name ?? string.Empty, parsed.FirstError.Description));
                }
                else
                {
                    modelName = parsed.Value;
                }
            }

            models.Add(new ModelDefinition { Name = new ModelRef(model.Name ?? string.Empty), Model = modelName });
        }

        if (modelErrors.Count > 0)
        {
            return modelErrors;
        }

        if (dto.Nodes.Count == 0)
        {
            return FlowErrors.Body(dto.Name ?? string.Empty, "流程缺少根节点。");
        }

        if (dto.Nodes.Count > 1)
        {
            return FlowErrors.Body(dto.Name ?? string.Empty, "流程只能有一个根节点。");
        }

        ErrorOr<IReadOnlyList<Port>> startOutputs = ToPorts(dto.Start?.Outputs, NodeScope.Flow(dto.Name!), StartNode.ReservedName.Value, allowValue: true);
        if (startOutputs.IsError)
        {
            return startOutputs.ErrorsOrEmptyList;
        }

        ErrorOr<NodeSpec> root = ToNode(dto.Nodes[0], NodeScope.Flow(dto.Name!));
        if (root.IsError)
        {
            return root.ErrorsOrEmptyList;
        }

        ErrorOr<FlowName> flowName = FlowName.Create(dto.Name!);
        if (flowName.IsError)
        {
            return flowName.ErrorsOrEmptyList;
        }

        return new FlowDefinition(flowName.Value, dto.Description, models, startOutputs.Value, root.Value);
    }

    /// <summary>装配节点的列表转换，错误逐条收集。</summary>
    private static ErrorOr<IReadOnlyList<NodeSpec>> ToNodes(List<NodeDto> nodes, NodeScope scope)
    {
        List<NodeSpec> result = [];
        List<Error> errors = [];

        foreach (NodeDto dto in nodes)
        {
            ErrorOr<NodeSpec> converted = ToNode(dto, scope);
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
    /// 省略的字段取安全默认值，合法性交校验。</summary>
    private static ErrorOr<NodeSpec> ToNode(NodeDto dto, NodeScope scope)
    {
        if (dto.Use is { Length: > 0 })
        {
            List<Error> useErrors = [];
            if (dto.Kind is not null)
            {
                useErrors.Add(scope.Node(dto.Name ?? string.Empty, "引用节点的类型由被引用的库定义决定，不写 Kind。"));
            }

            if (dto.Prompt is not null || dto.Tools is { Count: > 0 }
                || dto.Mode is not null || dto.Branch is not null
                || dto.Nodes is { Count: > 0 })
            {
                useErrors.Add(scope.Node(dto.Name ?? string.Empty, "引用成员不能同时声明执行配置或子节点。"));
            }

            if (dto.Model is not null && HasModels(dto.Models))
            {
                useErrors.Add(scope.Node(dto.Name ?? string.Empty, "引用节点不能同时声明模型与模型绑定。"));
            }

            if (dto.Inputs is { Count: > 0 })
            {
                useErrors.Add(scope.Node(dto.Name ?? string.Empty, "引用节点不能声明输入端口，端口声明属于容器定义。"));
            }

            if (dto.Outputs is { Count: > 0 } || dto.SystemPrompt is { Count: > 0 })
            {
                useErrors.Add(scope.Node(dto.Name ?? string.Empty, "引用节点不能声明输出端口与系统指令。"));
            }

            if (dto.Gate is not null)
            {
                useErrors.Add(scope.Node(dto.Name ?? string.Empty, "引用节点不能声明 Gate，门控由库定义决定。"));
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
                    useErrors.Add(scope.Node(dto.Name ?? string.Empty, parsedPredicate.FirstError.Description));
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

            ErrorOr<Dictionary<ModelRef, ModelRef>?> resolvedModels = ToModelBindings(dto.Models, scope, dto.Name ?? string.Empty);
            if (resolvedModels.IsError)
            {
                useErrors.AddRange(resolvedModels.ErrorsOrEmptyList);
                return useErrors;
            }

            ErrorOr<IReadOnlyList<SourceRef>> resolvedFrom = ToFrom(dto.From, scope, dto.Name ?? string.Empty);
            if (resolvedFrom.IsError)
            {
                return resolvedFrom.ErrorsOrEmptyList;
            }

            return new NodeSpec
            {
                Name = new NodeName(dto.Name ?? string.Empty),
                Kind = null,
                Use = new NodeName(dto.Use),
                Gate = dto.Gate ?? NodeGate.Auto,
                From = resolvedFrom.Value,
                Validate = declareValidation,
                MaxRuns = dto.MaxRuns,
                In = resolvedIn.Value,
                Model = ToModel(dto.Model),
                Models = resolvedModels.Value,
            };
        }

        if (dto.Kind is not { } kind)
        {
            return scope.Node(dto.Name ?? string.Empty, "节点必须声明 Kind，取值 Container、Executable 或 Input。");
        }

        if (kind == NodeKind.Container)
        {
            List<Error> containerErrors = [];
            if (dto.Prompt is not null)
            {
                containerErrors.Add(scope.Node(dto.Name ?? string.Empty, "容器节点不是执行节点，不支持 Prompt。"));
            }

            if (dto.Mode is not null)
            {
                containerErrors.Add(scope.Node(dto.Name ?? string.Empty, $"容器不支持 Mode，收到 {dto.Mode}。"));
            }

            if (dto.From is { Count: > 0 })
            {
                containerErrors.Add(scope.Node(dto.Name ?? string.Empty, "容器节点不是执行节点，不支持 From。"));
            }

            if (dto.Model is not null || dto.Branch is not null
                || dto.Tools is { Count: > 0 })
            {
                containerErrors.Add(scope.Node(dto.Name ?? string.Empty, "容器节点不是执行节点，不支持执行配置。"));
            }

            IReadOnlyList<ModelRef>? declaredSlots = null;
            if (dto.Models is { } declaredModels)
            {
                if (!scope.IsLibrary)
                {
                    containerErrors.Add(scope.Node(dto.Name ?? string.Empty, "内联容器不声明模型槽位与模型绑定，槽位声明只属于节点库容器定义。"));
                }
                else if (declaredModels.ValueKind != JsonValueKind.Array)
                {
                    containerErrors.Add(scope.Node(dto.Name ?? string.Empty, "容器定义的模型槽位写槽位名数组。"));
                }
                else
                {
                    ErrorOr<IReadOnlyList<ModelRef>> slots = ToModelSlots(declaredModels, scope, dto.Name ?? string.Empty);
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
                containerErrors.Add(scope.Node(dto.Name ?? string.Empty, "容器节点不是执行节点，不支持输入端口绑定。"));
            }

            if (dto.Inputs is { Count: > 0 } && !scope.IsLibrary)
            {
                containerErrors.Add(scope.Node(dto.Name ?? string.Empty, "容器节点不能声明输入端口，端口声明属于节点库容器定义。"));
            }

            if (dto.SystemPrompt is { Count: > 0 })
            {
                containerErrors.Add(scope.Node(dto.Name ?? string.Empty, "容器节点不是执行节点，不支持系统指令。"));
            }

            if (dto.Validate is not null)
            {
                containerErrors.Add(scope.Node(dto.Name ?? string.Empty, "容器节点不是执行节点，不支持 Validate。"));
            }

            IReadOnlyList<NodeSpec> children = [];
            if (dto.Nodes is { Count: > 0 })
            {
                ErrorOr<IReadOnlyList<NodeSpec>> parsedChildren = ToNodes(dto.Nodes, scope);
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
                containerErrors.Add(scope.Node(dto.Name ?? string.Empty, "容器节点必须声明子节点。"));
            }

            ErrorOr<IReadOnlyList<ContainerInput>> inputs = ToContainerInputs(dto.Inputs, scope, dto.Name ?? string.Empty);
            if (inputs.IsError)
            {
                containerErrors.AddRange(inputs.ErrorsOrEmptyList);
            }

            ErrorOr<Dictionary<PortName, ContainerOutput>?> outputPorts = ToContainerOutputs(dto.Outputs, scope, dto.Name ?? string.Empty);
            if (outputPorts.IsError)
            {
                containerErrors.AddRange(outputPorts.ErrorsOrEmptyList);
            }

            if (containerErrors.Count > 0)
            {
                return containerErrors;
            }

            return new NodeSpec
            {
                Name = new NodeName(dto.Name ?? string.Empty),
                Kind = NodeKind.Container,
                Gate = dto.Gate ?? NodeGate.Auto,
                Inputs = inputs.Value,
                ContainerOutputs = outputPorts.Value,
                MaxRuns = dto.MaxRuns,
                ModelSlots = declaredSlots,
                Nodes = children,
            };
        }

        List<Error> leafErrors = [];
        if (dto.Nodes is { Count: > 0 })
        {
            leafErrors.Add(scope.Node(dto.Name ?? string.Empty, $"{kind} 节点不能声明子节点，子节点只属于容器。"));
        }

        if (dto.Inputs is { Count: > 0 })
        {
            leafErrors.Add(scope.Node(dto.Name ?? string.Empty, "执行节点不能声明输入端口，接线从引用处提供。"));
        }

        if (dto.In is { Count: > 0 })
        {
            leafErrors.Add(scope.Node(dto.Name ?? string.Empty,
                "执行节点不写输入端口绑定，上下文输入用 From 条目的 Context 标记。"));
        }

        if (HasModels(dto.Models))
        {
            leafErrors.Add(scope.Node(dto.Name ?? string.Empty, "执行节点不声明模型槽位与模型绑定，槽位声明只属于容器定义。"));
        }

        foreach (string toolPath in dto.Tools ?? [])
        {
            if (ToolPath.InvalidReason(toolPath) is { } reason)
            {
                leafErrors.Add(scope.Node(dto.Name ?? string.Empty, reason));
            }
        }

        ErrorOr<NodeMode> leafMode = ParseNodeMode(dto.Mode);
        if (leafMode.IsError)
        {
            leafErrors.Add(scope.Node(dto.Name ?? string.Empty, leafMode.FirstError.Description));
        }

        OutputValidation? declaredValidation = null;
        if (dto.Validate is { } leafValidate)
        {
            ErrorOr<ValidationPredicate> parsedPredicate = ParseValidationPredicate(leafValidate.Predicate);
            if (parsedPredicate.IsError)
            {
                leafErrors.Add(scope.Node(dto.Name ?? string.Empty, parsedPredicate.FirstError.Description));
            }
            else
            {
                declaredValidation = new OutputValidation(parsedPredicate.Value, leafValidate.Argument);
            }
        }

        ErrorOr<IReadOnlyList<Port>> outputs = ToPorts(dto.Outputs, scope, dto.Name ?? string.Empty, allowValue: false);
        if (outputs.IsError)
        {
            leafErrors.AddRange(outputs.ErrorsOrEmptyList);
        }

        if (leafErrors.Count > 0)
        {
            return leafErrors;
        }

        ErrorOr<IReadOnlyList<SourceRef>> parsedFrom = ToFrom(dto.From, scope, dto.Name ?? string.Empty);
        if (parsedFrom.IsError)
        {
            return parsedFrom.ErrorsOrEmptyList;
        }

        return new NodeSpec
        {
            Name = new NodeName(dto.Name ?? string.Empty),
            Kind = kind,
            Gate = dto.Gate ?? NodeGate.Auto,
            From = parsedFrom.Value,
            Model = ToModel(dto.Model),
            Outputs = outputs.Value,
            SystemPrompt = [.. dto.SystemPrompt ?? []],
            Execution = new ExecutableSpec
            {
                Tools = [.. (dto.Tools ?? []).Select(name => new ToolPath(name))],
                Prompt = dto.Prompt,
                Question = dto.Question,
                Mode = leafMode.Value,
                Branch = dto.Branch is { Length: > 0 } branch ? new BranchName(branch) : null,
                Validate = declaredValidation,
                MaxRuns = dto.MaxRuns,
            },
        };
    }
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

            NodeName source = new(text[..at]);
            return PortRef.Named(source, port.Value);
        }

        return Error.Validation(ErrorCodes.FlowNode,
            $"端口引用 {text} 缺少 @，必须写 来源@端口 或 @端口。");
    }

    /// <summary>解析输出端口表：条目写 Name 与 Kind，Kind 必填，说明可写 Description。
    /// allowValue 为真时接受开始端口的固定值 Value。</summary>
    private static ErrorOr<IReadOnlyList<Port>> ToPorts(List<JsonElement>? entries, NodeScope scope, string nodeName, bool allowValue)
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
                errors.Add(scope.Node(nodeName, "输出端口条目必须是带 Name 与 Kind 的对象，说明写 Description。"));
                continue;
            }

            string? declaredName = null;
            PortKind? declaredKind = null;
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
                            errors.Add(scope.Node(nodeName, "输出端口的 Name 键必须是端口名。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredName = property.Value.GetString();
                        }
                        break;

                    case "kind":
                        if (property.Value.ValueKind != JsonValueKind.String
                            || !Enum.TryParse(property.Value.GetString(), ignoreCase: true, out PortKind parsedKind))
                        {
                            errors.Add(scope.Node(nodeName, "输出端口的 Kind 应为 Data、Items 或 Context。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredKind = parsedKind;
                        }
                        break;

                    case "description":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeName, "输出端口的 Description 键必须是说明文本。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredDescription = property.Value.GetString();
                        }
                        break;

                    case "ref":
                        errors.Add(scope.Node(nodeName, "执行节点的输出端口条目写 Name 与 Kind，不写 Ref。"));
                        invalid = true;
                        break;

                    case "value" when allowValue:
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeName, "开始端口的 Value 键必须是文本。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredValue = property.Value.GetString();
                        }
                        break;

                    case "value":
                        errors.Add(scope.Node(nodeName, "输出端口不认识键 Value，Value 只属于开始端口。"));
                        invalid = true;
                        break;

                    default:
                        errors.Add(scope.Node(nodeName, allowValue
                            ? $"输出端口不认识键 {property.Name}，条目写 Name、Kind、Description 与 Value。"
                            : $"输出端口不认识键 {property.Name}，条目写 Name、Kind 与 Description。"));
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

            if (declaredKind is null)
            {
                errors.Add(scope.Node(nodeName, "输出端口的 Kind 不能省略，必须是 Data、Items 或 Context。"));
                continue;
            }

            result.Add(new Port(parsedName.Value, declaredKind.Value, ToDescription(declaredDescription), declaredValue));
        }

        return errors.Count > 0 ? errors : result;
    }

    /// <summary>解析容器传入端口表：条目写 Name 与可选 Description，成员用 @端口名 引用。</summary>
    private static ErrorOr<IReadOnlyList<ContainerInput>> ToContainerInputs(
        List<JsonElement>? entries,
        NodeScope scope,
        string nodeName)
    {
        if (entries is not { Count: > 0 })
        {
            return Array.Empty<ContainerInput>();
        }

        List<ContainerInput> result = [];
        List<Error> errors = [];
        foreach (JsonElement entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                errors.Add(scope.Node(nodeName, "容器传入端口条目必须是带 Name 的对象，说明写 Description。"));
                continue;
            }

            string? declaredName = null;
            string? declaredDescription = null;
            bool invalid = false;
            foreach (JsonProperty property in entry.EnumerateObject())
            {
                switch (property.Name.ToLowerInvariant())
                {
                    case "name":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeName, "容器传入端口的 Name 键必须是端口名。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredName = property.Value.GetString();
                        }
                        break;

                    case "description":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeName, "容器传入端口的 Description 键必须是说明文本。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredDescription = property.Value.GetString();
                        }
                        break;

                    default:
                        errors.Add(scope.Node(nodeName, $"容器传入端口不认识键 {property.Name}，条目写 Name 与 Description。"));
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

            result.Add(new ContainerInput(parsedName.Value, ToDescription(declaredDescription)));
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
        string nodeName)
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
                errors.Add(scope.Node(nodeName, "容器输出端口条目必须是带 Name 与 Ref 的对象，说明写 Description。"));
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
                            errors.Add(scope.Node(nodeName, "容器输出端口的 Name 键必须是端口名。"));
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
                            errors.Add(scope.Node(nodeName, "容器输出端口的 Ref 键必须是 成员@端口 引用。"));
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
                            errors.Add(scope.Node(nodeName, "容器输出端口的 Description 键必须是说明文本。"));
                            invalid = true;
                        }
                        else
                        {
                            declaredDescription = property.Value.GetString();
                        }
                        break;

                    case "kind":
                        errors.Add(scope.Node(nodeName, "容器输出端口条目写 Name 与 Ref，不写 Kind。"));
                        invalid = true;
                        break;

                    default:
                        errors.Add(scope.Node(nodeName, $"容器输出端口不认识键 {property.Name}，条目写 Name、Ref 与 Description。"));
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
                errors.Add(scope.Node(nodeName, "容器输出端口的 Ref 不能省略，写 成员@端口。"));
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
                errors.Add(scope.Node(nodeName, $"容器输出端口 {parsedName.Value.Value} 重复。"));
            }
        }

        return errors.Count > 0 ? errors : result;
    }

    private static ModelRef? ToModel(string? model) =>
        string.IsNullOrWhiteSpace(model) ? null : new ModelRef(model);

    /// <summary>是否写了 Models 键。JSON 缺键时 JsonElement 为 Undefined。</summary>
    private static bool HasModels(JsonElement? models) =>
        models is { ValueKind: not JsonValueKind.Undefined };

    /// <summary>容器定义的模型槽位声明：字符串数组，名字不能为空且不重复。</summary>
    private static ErrorOr<IReadOnlyList<ModelRef>> ToModelSlots(JsonElement models, NodeScope scope, string nodeName)
    {
        List<ModelRef> slots = [];
        List<Error> errors = [];
        foreach (JsonElement entry in models.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(entry.GetString()))
            {
                errors.Add(scope.Node(nodeName, "模型槽位名不能为空。"));
                continue;
            }

            slots.Add(new ModelRef(entry.GetString()!));
        }

        foreach (ModelRef duplicated in slots.GroupBy(slot => slot).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            errors.Add(scope.Node(nodeName, $"模型槽位 {duplicated} 重复。"));
        }

        return errors.Count > 0 ? errors : slots;
    }

    /// <summary>引用节点组的模型绑定：对象形状，键是槽位名，值是流程模型选择名。</summary>
    private static ErrorOr<Dictionary<ModelRef, ModelRef>?> ToModelBindings(JsonElement? models, NodeScope scope, string nodeName)
    {
        if (!HasModels(models))
        {
            return (Dictionary<ModelRef, ModelRef>?)null;
        }

        if (models!.Value.ValueKind != JsonValueKind.Object)
        {
            return scope.Node(nodeName, "引用节点组的 Models 写槽位绑定对象，槽位声明只写容器定义。");
        }

        Dictionary<ModelRef, ModelRef> bindings = [];
        List<Error> errors = [];
        foreach (JsonProperty property in models.Value.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name)
                || property.Value.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(property.Value.GetString()))
            {
                errors.Add(scope.Node(nodeName, "模型绑定键与值都不能为空。"));
                continue;
            }

            bindings[new ModelRef(property.Name)] = new ModelRef(property.Value.GetString()!);
        }

        return errors.Count > 0 ? errors : bindings;
    }

    /// <summary>把模型槽位转回文件形状：容器定义写声明数组，引用节点组写绑定对象。</summary>
    private static JsonElement? ToModelsDto(NodeSpec node)
    {
        if (node.ModelSlots is { Count: > 0 } slots)
        {
            return JsonSerializer.SerializeToElement([.. slots.Select(slot => slot.Value)], FlowJson.Default.ListString);
        }

        if (node.Models is { Count: > 0 } bindings)
        {
            return JsonSerializer.SerializeToElement(
                bindings.ToDictionary(entry => entry.Key.Value, entry => entry.Value.Value),
                FlowJson.Default.DictionaryStringString);
        }

        return null;
    }

    /// <summary>把上游接线条目装配成模型：字符串条目是来源名，对象条目允许写 Node、Or、Signal、Context 键。
    /// 字段类型不符记入错误，不抛异常。</summary>
    private static ErrorOr<IReadOnlyList<SourceRef>> ToFrom(List<JsonElement>? entries, NodeScope scope, string nodeName)
    {
        if (entries is not { Count: > 0 })
        {
            return Array.Empty<SourceRef>();
        }

        List<SourceRef> result = [];
        List<Error> errors = [];
        foreach (JsonElement entry in entries)
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                ErrorOr<PortRef> parsedString = ParsePortRef(entry.GetString() ?? string.Empty);
                if (parsedString.IsError)
                {
                    errors.AddRange(parsedString.ErrorsOrEmptyList);
                }
                else
                {
                    result.Add(new SourceRef(parsedString.Value));
                }

                continue;
            }

            if (entry.ValueKind != JsonValueKind.Object)
            {
                errors.Add(scope.Node(nodeName, "From 条目必须是来源名或带 Node 键的对象。"));
                continue;
            }

            string? node = null;
            string? or = null;
            bool signal = false;
            bool context = false;
            foreach (JsonProperty property in entry.EnumerateObject())
            {
                switch (property.Name.ToLowerInvariant())
                {
                    case "node":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeName, "From 条目的 Node 键必须是来源名。"));
                        }
                        else
                        {
                            node = property.Value.GetString();
                        }
                        break;

                    case "or":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(scope.Node(nodeName, "From 条目的 Or 键必须是组名。"));
                        }
                        else
                        {
                            or = property.Value.GetString();
                        }
                        break;

                    case "signal":
                        if (property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                        {
                            errors.Add(scope.Node(nodeName, "From 条目的 Signal 键必须是布尔值。"));
                        }
                        else
                        {
                            signal = property.Value.GetBoolean();
                        }
                        break;

                    case "context":
                        if (property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                        {
                            errors.Add(scope.Node(nodeName, "From 条目的 Context 键必须是布尔值。"));
                        }
                        else
                        {
                            context = property.Value.GetBoolean();
                        }
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(node))
            {
                errors.Add(scope.Node(nodeName, "From 对象条目必须写 Node 键提供来源名。"));
                continue;
            }

            if (context && node.StartsWith('@'))
            {
                errors.Add(scope.Node(nodeName, "Context 条目来源必须写节点名或 来源@端口，不能引用绑定端口。"));
                continue;
            }

            if ((or is not null ? 1 : 0) + (signal ? 1 : 0) + (context ? 1 : 0) > 1)
            {
                errors.Add(scope.Node(nodeName, "From 条目不能同时写 Or、Signal、Context。"));
                continue;
            }

            ErrorOr<PortRef> parsedRef = ParsePortRef(node);
            if (parsedRef.IsError)
            {
                errors.AddRange(parsedRef.ErrorsOrEmptyList);
                continue;
            }

            result.Add(new SourceRef(parsedRef.Value, or, signal, context));
        }

        return errors.Count > 0 ? errors : result;
    }

    /// <summary>把谓词名按名字匹配枚举，只接受白名单名字，数字字面量在装配时拒绝。</summary>
    private static ErrorOr<ValidationPredicate> ParseValidationPredicate(string? predicate)
    {
        if (string.IsNullOrWhiteSpace(predicate))
        {
            return Error.Validation(ErrorCodes.FlowNode, "Validate.Predicate 不能为空。");
        }

        string? matched = Enum.GetNames<ValidationPredicate>()
            .FirstOrDefault(name => string.Equals(name, predicate, StringComparison.OrdinalIgnoreCase));

        return matched is not null
            ? Enum.Parse<Kuroe.Shared.Workflows.Flows.ValidationPredicate>(matched)
            : Error.Validation(ErrorCodes.FlowNode,
                $"Validate.Predicate 应为 NonEmpty、TextContains、TextNot、TextEquals 或 Pattern，收到 {predicate}。");
    }

    /// <summary>把节点定义与流程转回文件形状，保留引用形态。</summary>
    private static FlowFileDto ToDto(IReadOnlyList<NodeSpec> fileNodes, IReadOnlyList<FlowDefinition> flows) => new()
    {
        Nodes = [.. fileNodes.Select(ToNodeDto)],
        Flows = [.. flows.Select(ToFlowDto)],
    };

    private static FlowDto ToFlowDto(FlowDefinition flow) => new()
    {
        Name = flow.Name.Value,
        Description = flow.Description,
        Models = [.. flow.Models.Select(model => new ModelDto
        {
            Name = model.Name.Value,
            Model = model.Model?.Value,
        })],
        Start = new StartDto { Outputs = ToPortDtos(flow.StartOutputs) },
        Nodes = [ToNodeDto(flow.RootNode)],
    };

    /// <summary>把输出端口表转回文件形状：条目写 Name 与 Kind，说明写 Description。</summary>
    private static List<JsonElement>? ToPortDtos(IReadOnlyList<Port> ports)
    {
        if (ports.Count == 0)
        {
            return null;
        }

        List<JsonElement> entries = [];
        foreach (Port port in ports)
        {
            entries.Add(JsonSerializer.SerializeToElement(
                new PortEntryDto { Name = port.Name.Value, Kind = port.Kind, Description = port.Description, Value = port.Value },
                FlowJson.Default.PortEntryDto));
        }

        return entries;
    }

    /// <summary>把容器传入端口表转回文件形状：条目写 Name，说明写 Description。</summary>
    private static List<JsonElement>? ToContainerInputDtos(IReadOnlyList<ContainerInput> inputs)
    {
        List<JsonElement> entries = [];
        foreach (ContainerInput input in inputs)
        {
            entries.Add(JsonSerializer.SerializeToElement(
                new PortEntryDto { Name = input.Name.Value, Description = input.Description },
                FlowJson.Default.PortEntryDto));
        }

        return entries;
    }

    /// <summary>把容器输出端口表转回文件形状：条目写 Name 与 Ref，说明写 Description。</summary>
    private static List<JsonElement>? ToContainerOutputDtos(IReadOnlyDictionary<PortName, ContainerOutput> outputs)
    {
        List<JsonElement> entries = [];
        foreach ((PortName name, ContainerOutput output) in outputs)
        {
            entries.Add(JsonSerializer.SerializeToElement(
                new PortEntryDto { Name = name.Value, Ref = output.Ref.Display, Description = output.Description },
                FlowJson.Default.PortEntryDto));
        }

        return entries;
    }

    private static NodeDto ToNodeDto(NodeSpec node)
    {
        NodeDto dto = new()
        {
            Name = node.Name.Value,
            Kind = node.Kind,
            Use = node.Use?.Value,
            Gate = node.Gate == NodeGate.Auto ? null : node.Gate,
            From = node.From.Count == 0 ? null : [.. node.From.Select(ToFromDto)],
            Validate = ToValidationDto(node.Validate),
            MaxRuns = node.MaxRuns,
            Inputs = node.Inputs.Count == 0 ? null : ToContainerInputDtos(node.Inputs),
            Outputs = node.ContainerOutputs is { Count: > 0 } containerOutputs
                ? ToContainerOutputDtos(containerOutputs)
                : ToPortDtos(node.Outputs),
            SystemPrompt = node.SystemPrompt.Count == 0 ? null : [.. node.SystemPrompt],
            In = node.In is { Count: > 0 } inBindings
                ? inBindings.ToDictionary(entry => entry.Key.Value, entry => entry.Value.Display)
                : null,
            Model = node.Model?.Value,
            Models = ToModelsDto(node),
        };

        if (node.Nodes is { Count: > 0 })
        {
            dto.Nodes = [.. node.Nodes.Select(ToNodeDto)];
        }

        if (node.Execution is { } execution)
        {
            dto.Tools = execution.Tools.Count == 0 ? null : [.. execution.Tools.Select(path => path.Value)];
            dto.Prompt = execution.Prompt;
            dto.Question = execution.Question;
            dto.Mode = execution.Mode.ToString();
            dto.Branch = execution.Branch?.Value;
            dto.Validate = ToValidationDto(execution.Validate);
            dto.MaxRuns = execution.MaxRuns;
        }

        return dto;
    }

    /// <summary>把一条上游接线条目转回文件形状：无标记条目按来源名写字符串，带标记条目写对象。</summary>
    private static JsonElement ToFromDto(SourceRef source)
    {
        if (source.Or is null && !source.Signal && !source.Context)
        {
            return JsonSerializer.SerializeToElement(source.Ref.Display, FlowJson.Default.String);
        }

        return JsonSerializer.SerializeToElement(new FromEntryDto
        {
            Node = source.Ref.Display,
            Or = source.Or,
            Signal = source.Signal ? true : null,
            Context = source.Context ? true : null,
        }, FlowJson.Default.FromEntryDto);
    }

    /// <summary>把模型层的输出校验转回文件形状。</summary>
    private static ValidationDto? ToValidationDto(OutputValidation? validation) =>
        validation is null ? null : new ValidationDto { Predicate = validation.Predicate.ToString(), Argument = validation.Argument };

    /// <summary>执行节点模式的名字按枚举解析，未写时回退 `Single` 模式。</summary>
    private static ErrorOr<NodeMode> ParseNodeMode(string? mode)
    {
        if (mode is null)
        {
            return NodeMode.Single;
        }

        return Enum.TryParse(mode, ignoreCase: true, out NodeMode parsed)
            ? parsed
            : Error.Validation(ErrorCodes.FlowNode, $"Mode 应为 Single 或 PerItem，收到 {mode}。");
    }
}

/// <summary>一次装配的结果：叠加文件定义后的生效节点库、文件来源的节点与流程模型，以及展开后的流程。</summary>
internal sealed record AssembledFlows(
    NodeLibrary Library,
    IReadOnlyList<NodeSpec> FileNodes,
    IReadOnlyList<FlowDefinition> FlowModels,
    IReadOnlyList<FlowDefinition> Flows);
