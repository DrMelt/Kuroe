using System.Text.Json;
using ErrorOr;
using Kuroe.Shared;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Storage;

namespace Kuroe.Workflows.Flows;

/// <summary>flows.json 的读写。文件形状由 DTO 承载，绑定成 <see cref="Workflow"/> 后交校验。</summary>
sealed class WorkflowStore(string file)
{
    private readonly string _file = Path.GetFullPath(file);

    /// <summary>流程文件所在目录，用户给出的文件参数以此为基准。</summary>
    public string BaseDirectory => Path.GetDirectoryName(_file)!;

    /// <summary>把文件参数解析为绝对路径，相对参数按流程文件所在目录解析。</summary>
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

    /// <summary>读取流程文件，文件不存在时得到内置流程。</summary>
    public ErrorOr<List<Workflow>> Load()
    {
        if (!File.Exists(_file))
        {
            return new List<Workflow> { DefaultFlows.Builtin };
        }

        return Read(_file);
    }

    /// <summary>把全部流程写回文件。</summary>
    public ErrorOr<Success> Save(IReadOnlyList<Workflow> flows) => Write(_file, flows);

    /// <summary>从指定文件读取流程，供导入使用。</summary>
    public static ErrorOr<List<Workflow>> Read(string path)
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

        return parsed is null ? [WorkflowErrors.Format($"{path} 是空文件。")] : ToWorkflows(parsed);
    }

    private static ErrorOr<Success> Write(string path, IReadOnlyList<Workflow> flows)
    {
        FlowFileDto file = new()
        {
            Flows = [.. flows.Select(ToDto)],
        };

        try
        {
            AtomicFile.WriteText(path,
                JsonSerializer.Serialize(file, FlowJson.WriteOptions.GetTypeInfo(typeof(FlowFileDto))));

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [WorkflowErrors.Write(path, ex.Message)];
        }
    }

    private static ErrorOr<List<Workflow>> ToWorkflows(FlowFileDto file)
    {
        List<Workflow> flows = [];
        List<Error> errors = [];

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

        return errors.Count > 0 ? errors : flows;
    }

    private static ErrorOr<Workflow> ToWorkflow(WorkflowDto dto)
    {
        List<AgentDefinition> agents = [.. dto.Agents.Select(agent => new AgentDefinition
        {
            Name = agent.Name ?? string.Empty,
            Description = agent.Description,
            SystemPrompt = agent.SystemPrompt,
            Model = agent.Model,
            Tools = agent.Tools ?? [],
        })];

        ErrorOr<IReadOnlyList<NodeSpec>> nodes = ToNodes(dto.Nodes, dto.Name!);

        return nodes.IsError
            ? nodes.ErrorsOrEmptyList
            : new Workflow(dto.Name!, dto.Description, agents, nodes.Value);
    }

    /// <summary>有子节点的是容器，否则是叶子。缺省的字段取节点定义的安全值，Mode 非法时给出错误。</summary>
    private static ErrorOr<IReadOnlyList<NodeSpec>> ToNodes(List<NodeDto> nodes, string flowName)
    {
        List<NodeSpec> result = [];
        List<Error> errors = [];

        foreach (NodeDto node in nodes)
        {
            ErrorOr<NodeSpec> converted = ToNode(node, flowName);
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

    private static ErrorOr<NodeSpec> ToNode(NodeDto node, string flowName)
    {
        if (node.Nodes is { Count: > 0 })
        {
            ErrorOr<IReadOnlyList<NodeSpec>> children = ToNodes(node.Nodes, flowName);
            ErrorOr<ContainerMode> mode = ParseContainerMode(node.Mode);

            if (children.IsError || mode.IsError)
            {
                List<Error> errors = [];
                errors.AddRange(children.IsError ? children.ErrorsOrEmptyList : []);
                if (mode.IsError)
                {
                    errors.Add(WorkflowErrors.Node(flowName, node.Name ?? string.Empty, mode.FirstError.Description));
                }

                return errors;
            }

            return new FlowNode
            {
                Name = node.Name ?? string.Empty,
                Prompt = node.Prompt,
                From = node.From ?? [],
                Mode = mode.Value,
                Nodes = children.Value,
            };
        }

        ErrorOr<NodeMode> leafMode = ParseNodeMode(node.Mode);
        if (leafMode.IsError)
        {
            return [WorkflowErrors.Node(flowName, node.Name ?? string.Empty, leafMode.FirstError.Description)];
        }

        return new AgentNode
        {
            Name = node.Name ?? string.Empty,
            Agent = node.Agent ?? string.Empty,
            Prompt = node.Prompt,
            From = node.From ?? [],
            Output = node.Output ?? NodeOutput.Plain,
            Mode = leafMode.Value,
            Gate = node.Gate ?? NodeGate.Auto,
            OnReject = node.OnReject,
            MaxAttempts = node.MaxAttempts,
            Split = ToSplit(node.Split),
        };
    }

    /// <summary>把文件里的拆分配置装配成模型：缺失的必填字段留空交校验。</summary>
    private static SplitConfig? ToSplit(SplitDto? split) => split is null ? null : new SplitConfig(
        split.Items?.Select(item => new SplitItem(
            item.Title ?? string.Empty,
            item.Instruction ?? string.Empty,
            item.Acceptance ?? string.Empty,
            item.Branch)).ToList(),
        split.ExtrasMax,
        split.Acceptance);
    private static WorkflowDto ToDto(Workflow flow) => new()
    {
        Name = flow.Name,
        Description = flow.Description,
        Agents = [.. flow.Agents.Select(agent => new AgentDto
        {
            Name = agent.Name,
            Description = agent.Description,
            SystemPrompt = agent.SystemPrompt,
            Model = agent.Model,
            Tools = agent.Tools.Count == 0 ? null : [.. agent.Tools],
        })],
        Nodes = [.. flow.Nodes.Select(ToNodeDto)],
    };

    private static NodeDto ToNodeDto(NodeSpec node) => node switch
    {
        FlowNode flow => new NodeDto
        {
            Name = flow.Name,
            Prompt = flow.Prompt,
            From = flow.From.Count == 0 ? null : [.. flow.From],
            Mode = flow.Mode.ToString(),
            Nodes = [.. flow.Nodes.Select(ToNodeDto)],
        },
        AgentNode leaf => new NodeDto
        {
            Name = leaf.Name,
            Agent = leaf.Agent,
            Prompt = leaf.Prompt,
            From = leaf.From.Count == 0 ? null : [.. leaf.From],
            Output = leaf.Output,
            Mode = leaf.Mode.ToString(),
            Gate = leaf.Gate,
            OnReject = leaf.OnReject,
            MaxAttempts = leaf.MaxAttempts,
            Split = leaf.Split is { } split ? new SplitDto
            {
                Items = split.Items?.Select(item => new SplitItemDto
                {
                    Title = item.Title,
                    Instruction = item.Instruction,
                    Acceptance = item.Acceptance,
                    Branch = item.Branch,
                }).ToList(),
                ExtrasMax = split.ExtrasMax,
                Acceptance = split.Acceptance,
            } : null,
        },
        _ => throw new InvalidOperationException($"未知节点类型：{node.GetType().Name}"),
    };

    /// <summary>容器模式的名字按枚举解析，未写时回退顺序模式。</summary>
    private static ErrorOr<ContainerMode> ParseContainerMode(string? mode)
    {
        if (mode is null)
        {
            return ContainerMode.Sequential;
        }

        return Enum.TryParse(mode, ignoreCase: true, out ContainerMode parsed)
            ? parsed
            : Error.Validation(ErrorCodes.WorkflowNode, $"Mode 应为 Sequential 或 Parallel，收到 {mode}。");
    }

    /// <summary>叶子模式的名字按枚举解析，未写时回退整叶模式。</summary>
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