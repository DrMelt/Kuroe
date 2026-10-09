using System.Text.Json;
using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Shared;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Storage;
using ModelDefinition = Kuroe.Shared.Workflows.Flows.ModelDefinition;

namespace Kuroe.Workflows.Flows;

/// <summary>flows.json 的读写。文件形状由 DTO 承载，绑定成 <see cref="FlowFile"/> 后交校验与展开。</summary>
sealed class FlowStore(string file, string baseDirectory)
{
    /// <summary>节点库定义装配用作用域名。输入端口声明只属于库容器定义，装配层不允许。</summary>
    private const string LibraryScope = "节点库";

    private readonly string _file = Path.GetFullPath(file);
    private readonly string _baseDirectory = Path.GetFullPath(baseDirectory);

    /// <summary>工作目录根，用户给出的文件参数以此为基准。</summary>
    public string BaseDirectory => _baseDirectory;

    /// <summary>把文件参数解析为绝对路径，相对参数按工作目录解析。</summary>
    public ErrorOr<string> Resolve(string path)
    {
        try
        {
            return Path.GetFullPath(path, BaseDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return [FlowErrors.InvalidPath(path, ex.Message)];
        }
    }

    /// <summary>读取流程文件，文件不存在时得到内置内容。</summary>
    public ErrorOr<FlowFile> Load()
    {
        if (!File.Exists(_file))
        {
            return DefaultFlows.Builtin;
        }

        return Read(_file);
    }

    /// <summary>把流程内容写回文件。</summary>
    public ErrorOr<Success> Save(FlowFile file) => Write(_file, file);

    /// <summary>从指定文件读取流程内容，供导入使用。</summary>
    public static ErrorOr<FlowFile> Read(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [FlowErrors.Read(path, ex.Message)];
        }

        FlowFileDto? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(text, FlowJson.Default.FlowFileDto);
        }
        catch (JsonException ex)
        {
            return [FlowErrors.Format($"{path}：{ex.Message}")];
        }

        return parsed is null ? [FlowErrors.Format($"{path} 是空文件。")] : ToFile(parsed);
    }

    private static ErrorOr<Success> Write(string path, FlowFile file)
    {
        try
        {
            AtomicFile.WriteText(path,
                JsonSerializer.Serialize(ToDto(file), FlowJson.WriteOptions.GetTypeInfo(typeof(FlowFileDto))));

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [FlowErrors.Write(path, ex.Message)];
        }
    }

    private static ErrorOr<FlowFile> ToFile(FlowFileDto file)
    {
        List<NodeSpec> library = [];
        List<Error> errors = [];
        foreach (NodeDto dto in file.Nodes)
        {
            ErrorOr<NodeSpec> converted = ToNode(dto, LibraryScope);
            if (converted.IsError)
            {
                errors.AddRange(converted.ErrorsOrEmptyList);
            }
            else
            {
                library.Add(converted.Value);
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

        return errors.Count > 0 ? errors : new FlowFile(library, flows);
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

        ErrorOr<NodeSpec> root = ToNode(dto.Nodes[0], dto.Name!);
        if (root.IsError)
        {
            return root.ErrorsOrEmptyList;
        }

        ErrorOr<FlowName> flowName = FlowName.Create(dto.Name!);
        if (flowName.IsError)
        {
            return flowName.ErrorsOrEmptyList;
        }

        return new FlowDefinition(flowName.Value, dto.Description, models, root.Value);
    }

    /// <summary>装配节点的列表转换，错误逐条收集。</summary>
    private static ErrorOr<IReadOnlyList<NodeSpec>> ToNodes(List<NodeDto> nodes, string scope)
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

    /// <summary>把文件里的节点装配成模型：写有 Use 即引用库定义，否则按有无 Nodes 分容器与执行节点。
    /// 省略的字段取安全默认值，合法性交校验。</summary>
    private static ErrorOr<NodeSpec> ToNode(NodeDto dto, string scope)
    {
        if (dto.Use is { Length: > 0 })
        {
            List<Error> useErrors = [];
            if (dto.Prompt is not null || dto.Tools is { Count: > 0 }
                || dto.Output is not null || dto.Mode is not null || dto.Branch is not null
                || dto.Split is not null
                || dto.Nodes is { Count: > 0 })
            {
                useErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "引用成员不能同时声明执行配置或子节点。"));
            }

            if (dto.Model is not null && dto.Models is not null)
            {
                useErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "引用节点不能同时声明模型与模型绑定。"));
            }

            if (dto.Inputs is { Count: > 0 })
            {
                useErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "引用节点不能声明输入端口，端口声明属于容器定义。"));
            }

            if (dto.Out is { Count: > 0 })
            {
                useErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "引用节点不能声明输出端口，输出端口随容器定义。"));
            }

            if (dto.Outputs is { Count: > 0 } || dto.SystemPrompt is { Count: > 0 })
            {
                useErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "引用节点不能声明输出端口与系统指令。"));
            }

            if (dto.Gate is not null)
            {
                useErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "引用节点不能声明 Gate，门控由库定义决定。"));
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
                    useErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, parsedPredicate.FirstError.Description));
                    return useErrors;
                }

                declareValidation = new OutputValidation(parsedPredicate.Value, validateDeclared.Argument);
            }

            ErrorOr<Dictionary<PortName, NodeName>?> resolvedIn = ToIn(dto.In);
            if (resolvedIn.IsError)
            {
                useErrors.AddRange(resolvedIn.ErrorsOrEmptyList);
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
                Use = new NodeName(dto.Use),
                Gate = dto.Gate ?? NodeGate.Auto,
                From = resolvedFrom.Value,
                Validate = declareValidation,
                MaxRuns = dto.MaxRuns,
                In = resolvedIn.Value,
                Model = ToModel(dto.Model),
                Models = ToModelBindings(dto.Models),
            };
        }

        if (dto.Nodes is { Count: > 0 })
        {
            List<Error> containerErrors = [];
            if (dto.Prompt is not null)
            {
                containerErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不是执行节点，不支持 Prompt。"));
            }

            if (dto.Mode is not null)
            {
                containerErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, $"容器不支持 Mode，收到 {dto.Mode}。"));
            }

            if (dto.From is { Count: > 0 })
            {
                containerErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不是执行节点，不支持 From。"));
            }

            if (dto.Model is not null || dto.Output is not null || dto.Branch is not null
                || dto.Tools is { Count: > 0 } || dto.Split is not null)
            {
                containerErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不是执行节点，不支持执行配置。"));
            }

            if (dto.Models is not null)
            {
                containerErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不能声明模型绑定，模型绑定只用于引用节点组。"));
            }

            if (dto.In is { Count: > 0 })
            {
                containerErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不是执行节点，不支持输入端口绑定。"));
            }

            if (dto.Inputs is { Count: > 0 } && scope != LibraryScope)
            {
                containerErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不能声明输入端口，端口声明属于节点库容器定义。"));
            }

            if (dto.Out is { Count: > 0 } && scope != LibraryScope)
            {
                containerErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不能声明输出端口，输出端口声明属于节点库容器定义。"));
            }

            if (dto.Outputs is { Count: > 0 } || dto.SystemPrompt is { Count: > 0 })
            {
                containerErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不是执行节点，不支持输出端口与系统指令。"));
            }

            if (dto.Validate is not null)
            {
                containerErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不是执行节点，不支持 Validate。"));
            }

            ErrorOr<IReadOnlyList<NodeSpec>> children = ToNodes(dto.Nodes, scope);
            if (children.IsError)
            {
                containerErrors.AddRange(children.ErrorsOrEmptyList);
            }

            List<PortName> inputs = [];
            foreach (string name in dto.Inputs ?? [])
            {
                ErrorOr<PortName> port = PortName.Create(name);
                if (port.IsError)
                {
                    containerErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, port.FirstError.Description));
                }
                else
                {
                    inputs.Add(port.Value);
                }
            }

            ErrorOr<Dictionary<PortName, NodeName>?> outputPorts = ToOut(dto.Out);
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
                Gate = dto.Gate ?? NodeGate.Auto,
                Inputs = inputs,
                Out = outputPorts.Value,
                MaxRuns = dto.MaxRuns,
                Nodes = children.Value,
            };
        }

        List<Error> leafErrors = [];
        if (dto.Inputs is { Count: > 0 })
        {
            leafErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "执行节点不能声明输入端口，接线从引用处提供。"));
        }

        if (dto.Out is { Count: > 0 })
        {
            leafErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "执行节点不能声明容器输出端口，命名输出端口用 Outputs。"));
        }

        if (dto.In is { Count: > 0 })
        {
            leafErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty,
                "执行节点不写输入端口绑定，ContextInput 用 From 条目的 Context 标记。"));
        }

        if (dto.Models is not null)
        {
            leafErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, "执行节点不能声明模型绑定，模型绑定只用于引用节点组。"));
        }

        foreach (string toolPath in dto.Tools ?? [])
        {
            if (ToolPath.InvalidReason(toolPath) is { } reason)
            {
                leafErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, reason));
            }
        }

        ErrorOr<NodeMode> leafMode = ParseNodeMode(dto.Mode);
        if (leafMode.IsError)
        {
            leafErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, leafMode.FirstError.Description));
        }

        OutputValidation? declaredValidation = null;
        if (dto.Validate is { } leafValidate)
        {
            ErrorOr<ValidationPredicate> parsedPredicate = ParseValidationPredicate(leafValidate.Predicate);
            if (parsedPredicate.IsError)
            {
                leafErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, parsedPredicate.FirstError.Description));
            }
            else
            {
                declaredValidation = new OutputValidation(parsedPredicate.Value, leafValidate.Argument);
            }
        }

        List<PortName> outputs = [];
        foreach (string name in dto.Outputs ?? [])
        {
            ErrorOr<PortName> port = PortName.Create(name);
            if (port.IsError)
            {
                leafErrors.Add(FlowErrors.Node(scope, dto.Name ?? string.Empty, port.FirstError.Description));
            }
            else
            {
                outputs.Add(port.Value);
            }
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
            Gate = dto.Gate ?? NodeGate.Auto,
            From = parsedFrom.Value,
            Model = ToModel(dto.Model),
            Outputs = outputs,
            SystemPrompt = [.. dto.SystemPrompt ?? []],
            Execution = new ExecutableSpec
            {
                Tools = [.. (dto.Tools ?? []).Select(name => new ToolPath(name))],
                Prompt = dto.Prompt,
                Question = dto.Question,
                Output = dto.Output ?? NodeOutput.Text,
                Mode = leafMode.Value,
                Branch = dto.Branch is { Length: > 0 } branch ? new BranchName(branch) : null,
                Split = ToSplit(dto.Split),
                Validate = declaredValidation,
                MaxRuns = dto.MaxRuns,
            },
        };
    }
    /// <summary>解析容器输出端口表：键与值是「端口名到成员引用」，形状与输入绑定一致，走同一个装配。</summary>
    private static ErrorOr<Dictionary<PortName, NodeName>?> ToOut(Dictionary<string, string>? bindings) => ToIn(bindings);

    private static ErrorOr<Dictionary<PortName, NodeName>?> ToIn(Dictionary<string, string>? bindings)
    {
        if (bindings is not { Count: > 0 })
        {
            return (Dictionary<PortName, NodeName>?)null;
        }

        Dictionary<PortName, NodeName> result = [];
        List<Error> errors = [];
        foreach ((string key, string value) in bindings)
        {
            ErrorOr<PortName> port = PortName.Create(key);
            if (port.IsError)
            {
                errors.AddRange(port.ErrorsOrEmptyList);
            }
            else
            {
                result[port.Value] = new NodeName(value);
            }
        }

        return errors.Count > 0 ? errors : result;
    }

    private static ModelRef? ToModel(string? model) =>
        string.IsNullOrWhiteSpace(model) ? null : new ModelRef(model);

    private static Dictionary<ModelRef, ModelRef>? ToModelBindings(Dictionary<string, string>? bindings) =>
        bindings is { Count: > 0 }
            ? bindings.ToDictionary(entry => new ModelRef(entry.Key), entry => new ModelRef(entry.Value))
            : null;

    /// <summary>把上游接线条目装配成模型：字符串条目是来源名，对象条目允许写 Node、Or、Signal、Context 键。
    /// 字段类型不符记入错误，不抛异常。</summary>
    private static ErrorOr<IReadOnlyList<SourceRef>> ToFrom(List<JsonElement>? entries, string scope, string nodeName)
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
                result.Add(new SourceRef(new NodeName(entry.GetString() ?? string.Empty)));
                continue;
            }

            if (entry.ValueKind != JsonValueKind.Object)
            {
                errors.Add(FlowErrors.Node(scope, nodeName, "From 条目必须是来源名或带 Node 键的对象。"));
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
                            errors.Add(FlowErrors.Node(scope, nodeName, "From 条目的 Node 键必须是来源名。"));
                        }
                        else
                        {
                            node = property.Value.GetString();
                        }
                        break;

                    case "or":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            errors.Add(FlowErrors.Node(scope, nodeName, "From 条目的 Or 键必须是组名。"));
                        }
                        else
                        {
                            or = property.Value.GetString();
                        }
                        break;

                    case "signal":
                        if (property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                        {
                            errors.Add(FlowErrors.Node(scope, nodeName, "From 条目的 Signal 键必须是布尔值。"));
                        }
                        else
                        {
                            signal = property.Value.GetBoolean();
                        }
                        break;

                    case "context":
                        if (property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                        {
                            errors.Add(FlowErrors.Node(scope, nodeName, "From 条目的 Context 键必须是布尔值。"));
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
                errors.Add(FlowErrors.Node(scope, nodeName, "From 对象条目必须写 Node 键提供来源名。"));
                continue;
            }

            if (context && node.StartsWith('@'))
            {
                errors.Add(FlowErrors.Node(scope, nodeName, "Context 条目来源必须写节点名或 来源@端口，不能引用绑定端口。"));
                continue;
            }

            if ((or is not null ? 1 : 0) + (signal ? 1 : 0) + (context ? 1 : 0) > 1)
            {
                errors.Add(FlowErrors.Node(scope, nodeName, "From 条目不能同时写 Or、Signal、Context。"));
                continue;
            }

            result.Add(new SourceRef(new NodeName(node), or, signal, context));
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

    /// <summary>把文件里的拆分配置装配成模型：缺失的必填字段留空交校验。</summary>
    private static SplitConfig? ToSplit(SplitDto? split) => split is null ? null : new SplitConfig(
        split.Items?.Select(item => new SplitItem(
            item.Title ?? string.Empty,
            item.Instruction ?? string.Empty,
            item.Acceptance ?? string.Empty,
            item.Branch is { Length: > 0 } branch ? new BranchName(branch) : null)).ToList(),
        split.ExtrasMax,
        split.Acceptance);

    /// <summary>把流程内容转回文件形状，保留节点库与引用形态。</summary>
    private static FlowFileDto ToDto(FlowFile file) => new()
    {
        Nodes = [.. file.Nodes.Select(ToNodeDto)],
        Flows = [.. file.Flows.Select(ToFlowDto)],
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
        Nodes = [ToNodeDto(flow.RootNode)],
    };

    private static NodeDto ToNodeDto(NodeSpec node)
    {
        NodeDto dto = new()
        {
            Name = node.Name.Value,
            Use = node.Use?.Value,
            Gate = node.Gate,
            From = node.From.Count == 0 ? null : [.. node.From.Select(ToFromDto)],
            Validate = ToValidationDto(node.Validate),
            MaxRuns = node.MaxRuns,
            Inputs = node.Inputs.Count == 0 ? null : [.. node.Inputs.Select(name => name.Value)],
            Out = node.Out is { Count: > 0 } outputs
                ? outputs.ToDictionary(entry => entry.Key.Value, entry => entry.Value.Value)
                : null,
            Outputs = node.Outputs.Count == 0 ? null : [.. node.Outputs.Select(name => name.Value)],
            SystemPrompt = node.SystemPrompt.Count == 0 ? null : [.. node.SystemPrompt],
            In = node.In is { Count: > 0 } inBindings
                ? inBindings.ToDictionary(entry => entry.Key.Value, entry => entry.Value.Value)
                : null,
            Model = node.Model?.Value,
            Models = node.Models is { Count: > 0 } modelBindings
                ? modelBindings.ToDictionary(entry => entry.Key.Value, entry => entry.Value.Value)
                : null,
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
            dto.Output = execution.Output;
            dto.Mode = execution.Mode.ToString();
            dto.Branch = execution.Branch?.Value;
            dto.Validate = ToValidationDto(execution.Validate);
            dto.MaxRuns = execution.MaxRuns;
            dto.Split = execution.Split is { } split ? new SplitDto
            {
                Items = split.Items?.Select(item => new SplitItemDto
                {
                    Title = item.Title,
                    Instruction = item.Instruction,
                    Acceptance = item.Acceptance,
                    Branch = item.Branch?.Value,
                }).ToList(),
                ExtrasMax = split.ExtrasMax,
                Acceptance = split.Acceptance,
            } : null;
        }

        return dto;
    }

    /// <summary>把一条上游接线条目转回文件形状：无标记条目按来源名写字符串，带标记条目写对象。</summary>
    private static JsonElement ToFromDto(SourceRef source)
    {
        if (source.Or is null && !source.Signal && !source.Context)
        {
            return JsonSerializer.SerializeToElement(source.Name.Value, FlowJson.Default.String);
        }

        return JsonSerializer.SerializeToElement(new FromEntryDto
        {
            Node = source.Name.Value,
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
