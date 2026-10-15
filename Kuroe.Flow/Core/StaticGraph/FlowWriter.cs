using System.Text.Json;
using Kuroe.Flow.Json;
using Kuroe.Flow.Shared.Files;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.StaticGraph;

/// <summary>模型到流程文件形状的写入：把节点定义与流程转回 DTO，保留引用形态。</summary>
static class FlowWriter
{
    /// <summary>把节点定义与流程转回文件形状，保留引用形态，只写文件来源的节点定义。types 给出类型形状，决定字段与端口的写出。</summary>
    internal static FlowFileDto ToDto(
        IReadOnlyList<NodeSpec> fileNodes,
        IReadOnlyList<FlowDefinition> flows,
        NodeTypeRegistry types) => new()
        {
            Nodes = [.. fileNodes.Select(node => ToNodeDto(node, types))],
            Flows = [.. flows.Select(flow => ToFlowDto(flow, types))],
        };

    private static FlowDto ToFlowDto(FlowDefinition flow, NodeTypeRegistry types) => new()
    {
        Name = flow.Name.Value,
        Description = flow.Description,
        Models = [.. flow.Models.Select(model => new ModelDto
        {
            Name = model.Name.Value,
            Model = model.Model,
        })],
        Start = new StartDto { Outputs = ToPortDtos(flow.StartOutputs) },
        Nodes = [ToNodeDto(flow.RootNode, types)],
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

    private static NodeDto ToNodeDto(NodeSpec node, NodeTypeRegistry types)
    {
        NodeDto dto = new()
        {
            Name = node.Name.Value,
            Kind = node.Kind?.Value,
            Use = node.Use?.Value,
            From = node.From.Count == 0 ? null : [.. node.From.Select(ToFromDto)],
            Validate = ToValidationDto(node.Validate),
            MaxRuns = node.MaxRuns,
            Inputs = node.Inputs.Count == 0 ? null : ToInputDtos(node.Inputs),
            Outputs = OutputsFor(node, types),
            SystemPrompt = node.SystemPrompt.Count == 0 ? null : [.. node.SystemPrompt],
            In = node.In is { Count: > 0 } inBindings
                ? inBindings.ToDictionary(entry => entry.Key.Value, entry => entry.Value.Display)
                : null,
            Model = node.Model?.Value,
            Models = ToModelsDto(node),
        };

        if (node.Nodes is { Count: > 0 })
        {
            dto.Nodes = [.. node.Nodes.Select(child => ToNodeDto(child, types))];
        }

        if (node.Execution is { } execution)
        {
            // 对外输入的提示对所有类型有效，执行配置只属于派发 run 的类型
            dto.Question = execution.Question;

            if (ShapeOf(node, types) is { DispatchesRuns: true })
            {
                dto.Tools = execution.Tools.Count == 0 ? null : [.. execution.Tools.Select(path => path.Value)];
                dto.Prompt = execution.Prompt;
                dto.Mode = execution.Mode.ToString();
                dto.Branch = execution.Branch?.Value;
                dto.Validate = ToValidationDto(execution.Validate);
                dto.MaxRuns = execution.MaxRuns;
            }
        }

        return dto;
    }

    /// <summary>节点对外端口的文件形状：派生端口的类型不写输出端口，容器写转发条目，其余写执行节点输出端口。</summary>
    private static List<JsonElement>? OutputsFor(NodeSpec node, NodeTypeRegistry types)
    {
        if (ShapeOf(node, types) is { DerivesOutputsFromInputs: true })
        {
            return null;
        }

        return node.ContainerOutputs is { Count: > 0 } containerOutputs
            ? ToContainerOutputDtos(containerOutputs)
            : ToPortDtos(node.Outputs);
    }

    /// <summary>节点声明的类型形状，引用节点与未注册的类型为空。</summary>
    private static NodeTypeShape? ShapeOf(NodeSpec node, NodeTypeRegistry types) =>
        node.Kind is { } kind ? types.Of(kind)?.Shape : null;

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
