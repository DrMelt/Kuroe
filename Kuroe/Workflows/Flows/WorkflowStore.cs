using System.Text.Json;
using ErrorOr;
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

            flows.Add(ToWorkflow(dto));
        }

        return errors.Count > 0 ? errors : flows;
    }

    private static Workflow ToWorkflow(WorkflowDto dto)
    {
        List<AgentDefinition> agents = [.. dto.Agents.Select(agent => new AgentDefinition
        {
            Name = agent.Name ?? string.Empty,
            Description = agent.Description,
            SystemPrompt = agent.SystemPrompt,
            Model = agent.Model,
            Tools = agent.Tools ?? [],
        })];

        return new Workflow(dto.Name!, dto.Description, agents, ToNodes(dto.Nodes));
    }

    /// <summary>有子节点的是容器，否则是叶子。缺省的字段取节点定义的安全值，合法性交校验。</summary>
    private static IReadOnlyList<NodeSpec> ToNodes(List<NodeDto> nodes) =>
        [.. nodes.Select(ToNode)];

    private static NodeSpec ToNode(NodeDto node) => node.Nodes is { Count: > 0 }
        ? new FlowNode
        {
            Name = node.Name ?? string.Empty,
            Prompt = node.Prompt,
            From = node.From ?? [],
            Nodes = ToNodes(node.Nodes),
        }
        : new AgentNode
        {
            Name = node.Name ?? string.Empty,
            Agent = node.Agent ?? string.Empty,
            Prompt = node.Prompt,
            From = node.From ?? [],
            Output = node.Output ?? NodeOutput.Plain,
            Mode = node.Mode ?? NodeMode.Single,
            Gate = node.Gate ?? NodeGate.Auto,
            OnReject = node.OnReject,
            MaxAttempts = node.MaxAttempts,
        };
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
            Nodes = [.. flow.Nodes.Select(ToNodeDto)],
        },
        AgentNode leaf => new NodeDto
        {
            Name = leaf.Name,
            Agent = leaf.Agent,
            Prompt = leaf.Prompt,
            From = leaf.From.Count == 0 ? null : [.. leaf.From],
            Output = leaf.Output,
            Mode = leaf.Mode,
            Gate = leaf.Gate,
            OnReject = leaf.OnReject,
            MaxAttempts = leaf.MaxAttempts,
        },
        _ => throw new InvalidOperationException($"未知节点类型：{node.GetType().Name}"),
    };
}