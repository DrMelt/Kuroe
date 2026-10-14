using System.Text.Json;
using Kuroe.Flows.Json;
using Kuroe.Flows.Json.Model;
using Kuroe.Flows.Shared.StaticGraph;

namespace Kuroe.Flows.StaticGraph;

/// <summary>模型到流程文件形状的写入：把节点定义与流程转回 DTO，保留引用形态。</summary>
static class FlowWriter
{
    /// <summary>把节点定义与流程转回文件形状，保留引用形态，只写文件来源的节点定义。</summary>
    internal static FlowFileDto ToDto(IReadOnlyList<NodeSpec> fileNodes, IReadOnlyList<FlowDefinition> flows) => new()
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
            Model = model.Model,
        })],
        Start = new StartDto { Outputs = ToPortDtos(flow.StartOutputs) },
        Nodes = [ToNodeDto(flow.RootNode)],
    };

    /// <summary>把输出端口表转回文件形状：条目写 Name 与 Type，说明写 Description。</summary>
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
                new PortEntryDto { Name = port.Name.Value, Type = port.Type, Description = port.Description, Value = port.Value },
                FlowJson.Default.PortEntryDto));
        }

        return entries;
    }

    /// <summary>把输入端口表转回文件形状：条目写 Name 与 Type，说明写 Description。</summary>
    private static List<JsonElement>? ToInputDtos(IReadOnlyList<InputPort> inputs)
    {
        if (inputs.Count == 0)
        {
            return null;
        }

        List<JsonElement> entries = [];
        foreach (InputPort input in inputs)
        {
            entries.Add(JsonSerializer.SerializeToElement(
                new PortEntryDto { Name = input.Name.Value, Type = input.Type, Description = input.Description },
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
            Inputs = node.Inputs.Count == 0 ? null : ToInputDtos(node.Inputs),
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

    /// <summary>把一组上游连接转回文件形状：组内是输入名到来源端口引用的映射。</summary>
    private static JsonElement ToFromDto(FromGroup group) =>
        JsonSerializer.SerializeToElement(
            group.Links.ToDictionary(link => link.Input.Value, link => link.Ref.Display),
            FlowJson.Default.DictionaryStringString);

    /// <summary>把模型层的输出校验转回文件形状。</summary>
    private static ValidationDto? ToValidationDto(OutputValidation? validation) =>
        validation is null ? null : new ValidationDto { Predicate = validation.Predicate.ToString(), Argument = validation.Argument };

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
}