using System.Text.Json;
using ErrorOr;
using Kuroe.Shared;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Storage;

namespace Kuroe.Workflows.Flows;

/// <summary>flows.json 的读写。文件形状由 DTO 承载，绑定成 <see cref="FlowFile"/> 后交校验与展开。</summary>
sealed class WorkflowStore(string file, string baseDirectory)
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
            return [WorkflowErrors.InvalidPath(path, ex.Message)];
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
            return [WorkflowErrors.Read(path, ex.Message)];
        }

        FlowFileDto? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(text, FlowJson.Default.FlowFileDto);
        }
        catch (JsonException ex)
        {
            return [WorkflowErrors.Format($"{path}：{ex.Message}")];
        }

        return parsed is null ? [WorkflowErrors.Format($"{path} 是空文件。")] : ToFile(parsed);
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
            return [WorkflowErrors.Write(path, ex.Message)];
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

        List<Workflow> flows = [];
        foreach (WorkflowDto dto in file.Flows)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errors.Add(WorkflowErrors.MissingName());

                continue;
            }

            ErrorOr<Workflow> flow = ToWorkflow(dto);
            if (flow.IsError)
            {
                errors.AddRange(flow.ErrorsOrEmptyList);

                continue;
            }

            flows.Add(flow.Value);
        }

        return errors.Count > 0 ? errors : new FlowFile(library, flows);
    }
    private static ErrorOr<Workflow> ToWorkflow(WorkflowDto dto)
    {
        List<ModelDefinition> models = [.. dto.Models.Select(model => new ModelDefinition
        {
            Name = new ModelRef(model.Name ?? string.Empty),
            Model = model.Model,
        })];

        if (dto.Nodes.Count == 0)
        {
            return WorkflowErrors.Body(dto.Name ?? string.Empty, "流程缺少根节点。");
        }

        if (dto.Nodes.Count > 1)
        {
            return WorkflowErrors.Body(dto.Name ?? string.Empty, "流程只能有一个根节点。");
        }

        ErrorOr<NodeSpec> root = ToNode(dto.Nodes[0], dto.Name!);

        return root.IsError
            ? root.ErrorsOrEmptyList
            : new Workflow(dto.Name!, dto.Description, models, root.Value);
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
                useErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "引用成员不能同时声明执行配置或子节点。"));
            }

            if (dto.Model is not null && dto.Models is not null)
            {
                useErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "引用节点不能同时声明模型与模型绑定。"));
            }

            if (dto.Inputs is { Count: > 0 })
            {
                useErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "引用节点不能声明输入端口，端口声明属于容器定义。"));
            }

            if (dto.Gate is not null)
            {
                useErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "引用节点不能声明 Gate，门控由库定义决定。"));
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
                    useErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, parsedPredicate.FirstError.Description));
                    return useErrors;
                }

                declareValidation = new OutputValidation(parsedPredicate.Value, validateDeclared.Argument);
            }

            return new NodeSpec
            {
                Name = new NodeName(dto.Name ?? string.Empty),
                Use = new NodeName(dto.Use),
                Gate = dto.Gate ?? NodeGate.Auto,
                From = [.. (dto.From ?? []).Select(name => new NodeName(name))],
                AnyOf = ToAnyOf(dto.AnyOf),
                Validate = declareValidation,
                In = ToIn(dto.In),
                Model = ToModel(dto.Model),
                Models = ToModelBindings(dto.Models),
            };
        }

        if (dto.Nodes is { Count: > 0 })
        {
            List<Error> containerErrors = [];
            if (dto.Prompt is not null)
            {
                containerErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不是执行节点，不支持 Prompt。"));
            }

            if (dto.Mode is not null)
            {
                containerErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, $"容器不支持 Mode，收到 {dto.Mode}。"));
            }

            if (dto.From is { Count: > 0 })
            {
                containerErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不是执行节点，不支持 From。"));
            }

            if (dto.Model is not null || dto.Output is not null || dto.Branch is not null
                || dto.Tools is { Count: > 0 } || dto.Split is not null)
            {
                containerErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不是执行节点，不支持执行配置。"));
            }

            if (dto.Models is not null)
            {
                containerErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不能声明模型绑定，模型绑定只用于引用节点组。"));
            }

            if (dto.In is { Count: > 0 })
            {
                containerErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不是执行节点，不支持输入端口绑定。"));
            }

            if (dto.Inputs is { Count: > 0 } && scope != LibraryScope)
            {
                containerErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不能声明输入端口，端口声明属于节点库容器定义。"));
            }

            if (dto.AnyOf is { Count: > 0 })
            {
                containerErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不是执行节点，不支持 AnyOf。"));
            }

            if (dto.Validate is not null)
            {
                containerErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "容器节点不是执行节点，不支持 Validate。"));
            }

            ErrorOr<IReadOnlyList<NodeSpec>> children = ToNodes(dto.Nodes, scope);
            if (children.IsError)
            {
                containerErrors.AddRange(children.ErrorsOrEmptyList);
            }

            if (containerErrors.Count > 0)
            {
                return containerErrors;
            }

            return new NodeSpec
            {
                Name = new NodeName(dto.Name ?? string.Empty),
                Gate = dto.Gate ?? NodeGate.Auto,
                Inputs = [.. (dto.Inputs ?? []).Select(name => new NodeName(name))],
                Nodes = children.Value,
            };
        }

        List<Error> leafErrors = [];
        if (dto.Inputs is { Count: > 0 })
        {
            leafErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "执行节点不能声明输入端口，接线从引用处提供。"));
        }

        if (dto.In is { Count: > 0 })
        {
            leafErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "执行节点不能声明输入端口绑定。"));
        }

        if (dto.Models is not null)
        {
            leafErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, "执行节点不能声明模型绑定，模型绑定只用于引用节点组。"));
        }

        ErrorOr<NodeMode> leafMode = ParseNodeMode(dto.Mode);
        if (leafMode.IsError)
        {
            leafErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, leafMode.FirstError.Description));
        }

        OutputValidation? declaredValidation = null;
        if (dto.Validate is { } leafValidate)
        {
            ErrorOr<ValidationPredicate> parsedPredicate = ParseValidationPredicate(leafValidate.Predicate);
            if (parsedPredicate.IsError)
            {
                leafErrors.Add(WorkflowErrors.Node(scope, dto.Name ?? string.Empty, parsedPredicate.FirstError.Description));
            }
            else
            {
                declaredValidation = new OutputValidation(parsedPredicate.Value, leafValidate.Argument);
            }
        }

        if (leafErrors.Count > 0)
        {
            return leafErrors;
        }

        return new NodeSpec
        {
            Name = new NodeName(dto.Name ?? string.Empty),
            Gate = dto.Gate ?? NodeGate.Auto,
            From = [.. (dto.From ?? []).Select(name => new NodeName(name))],
            Model = ToModel(dto.Model),
            Execution = new ExecutableSpec
            {
                Tools = [.. (dto.Tools ?? []).Select(name => new ToolName(name))],
                Prompt = dto.Prompt,
                Output = dto.Output ?? NodeOutput.Plain,
                Mode = leafMode.Value,
                Branch = dto.Branch is { Length: > 0 } branch ? new BranchName(branch) : null,
                Split = ToSplit(dto.Split),
                AnyOf = ToAnyOf(dto.AnyOf),
                Validate = declaredValidation,
            },
        };
    }
    private static Dictionary<NodeName, NodeName>? ToIn(Dictionary<string, string>? bindings) =>
        bindings is { Count: > 0 } ? bindings.ToDictionary(entry => new NodeName(entry.Key), entry => new NodeName(entry.Value)) : null;

    private static ModelRef? ToModel(string? model) =>
        string.IsNullOrWhiteSpace(model) ? null : new ModelRef(model);

    private static Dictionary<ModelRef, ModelRef>? ToModelBindings(Dictionary<string, string>? bindings) =>
        bindings is { Count: > 0 }
            ? bindings.ToDictionary(entry => new ModelRef(entry.Key), entry => new ModelRef(entry.Value))
            : null;

    /// <summary>把可选启动条件组装配成模型：每组是节点名，引用沿展开作用域解析。</summary>
    private static IReadOnlyList<IReadOnlyList<NodeName>> ToAnyOf(List<List<string>>? groups)
    {
        if (groups is null)
        {
            return [];
        }

        return [.. groups.Select(group => (IReadOnlyList<NodeName>)[.. group.Select(name => new NodeName(name))])];
    }

    /// <summary>把谓词名按名字匹配枚举，只接受白名单名字，数字字面量在装配时拒绝。</summary>
    private static ErrorOr<ValidationPredicate> ParseValidationPredicate(string? predicate)
    {
        if (string.IsNullOrWhiteSpace(predicate))
        {
            return Error.Validation(ErrorCodes.WorkflowNode, "Validate.Predicate 不能为空。");
        }

        string? matched = Enum.GetNames<ValidationPredicate>()
            .FirstOrDefault(name => string.Equals(name, predicate, StringComparison.OrdinalIgnoreCase));

        return matched is not null
            ? Enum.Parse<Kuroe.Shared.Workflows.Flows.ValidationPredicate>(matched)
            : Error.Validation(ErrorCodes.WorkflowNode,
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
        Flows = [.. file.Flows.Select(ToWorkflowDto)],
    };

    private static WorkflowDto ToWorkflowDto(Workflow flow) => new()
    {
        Name = flow.Name,
        Description = flow.Description,
        Models = [.. flow.Models.Select(model => new ModelDto
        {
            Name = model.Name.Value,
            Model = model.Model,
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
            From = node.From.Count == 0 ? null : [.. node.From.Select(name => name.Value)],
            AnyOf = ToAnyOfDto(node.AnyOf),
            Validate = ToValidationDto(node.Validate),
            Inputs = node.Inputs.Count == 0 ? null : [.. node.Inputs.Select(name => name.Value)],
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
            dto.Tools = execution.Tools.Count == 0 ? null : [.. execution.Tools.Select(name => name.Value)];
            dto.Prompt = execution.Prompt;
            dto.Output = execution.Output;
            dto.Mode = execution.Mode.ToString();
            dto.Branch = execution.Branch?.Value;
            dto.AnyOf = ToAnyOfDto(execution.AnyOf);
            dto.Validate = ToValidationDto(execution.Validate);
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

    /// <summary>把模型层的可选启动条件组转回文件形状。</summary>
    private static List<List<string>>? ToAnyOfDto(IReadOnlyList<IReadOnlyList<NodeName>> groups) =>
        groups.Count == 0 ? null : [.. groups.Select(group => group.Select(name => name.Value).ToList())];

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
            : Error.Validation(ErrorCodes.WorkflowNode, $"Mode 应为 Single 或 PerItem，收到 {mode}。");
    }
}