using Kuroe.Presentation.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Node.Shared.StaticGraph.Flow;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.Control.Graph.Runtime.Catalog;

/// <summary>把流程定义投影为宿主可读的中性模板视图。</summary>
public static class FlowTemplateProjection
{
    /// <summary>按流程定义与宿主开始输入绑定生成模板视图。</summary>
    public static FlowTemplateView Project(
        FlowDefinition flow,
        IReadOnlyDictionary<PortName, string> hostInputs)
    {
        List<FlowStepView> steps = [];
        List<FlowGroupView> groups = [];
        Walk(flow.RootNode, null, steps, groups);

        return new FlowTemplateView(
            flow.Name.Value,
            flow.Description,
            [.. flow.StartPorts.Select(port => StartPort(port, hostInputs))],
            steps,
            groups);
    }

    private static void Walk(
        NodeSpec node,
        NodePath? parent,
        List<FlowStepView> steps,
        List<FlowGroupView> groups)
    {
        NodePath path = parent is { } parentPath ? parentPath.Append(node.Label) : NodePath.Of(node.Label.Value);

        if (node.IsContainer)
        {
            groups.Add(Group(node, path));
            foreach (NodeSpec child in node.Nodes ?? [])
            {
                Walk(child, path, steps, groups);
            }

            return;
        }

        if (node.Spec is not null)
        {
            steps.Add(Step(node, path));
        }
    }

    private static FlowStartPortView StartPort(
        Port port,
        IReadOnlyDictionary<PortName, string> hostInputs)
    {
        string name = port.Name.Value;
        string kind = port.Type.Name;
        string? description = port.Description;
        string? defaultValue = port.Default;
        string? value = port.Value;
        FlowStartKind inputKind = KindOf(StartInputRules.Kind(port, hostInputs.ContainsKey(port.Name)));
        string? hostInput = hostInputs.GetValueOrDefault(port.Name);

        return new FlowStartPortView(name, kind, description, defaultValue, value, inputKind, hostInput)
        {
            Constant = port.IsConstant,
        };
    }

    private static FlowStepView Step(
        NodeSpec node,
        NodePath path) =>
        new(
            path.Value,
            node.Label.Value,
            node.EffectiveKind.Value,
            FlowItemState.Pending,
            [.. node.EffectiveInputs.Select(port => Input(port, node))],
            [.. node.Outputs.Select(Output)],
            [],
            node.MaxExecutions ?? FlowStepView.DefaultMaxExecutions)
        {
            GroupPath = path.Parent?.Value,
            Model = Model(node),
            Attributes = [.. (node.Type?.Attributes ?? []).Select(attribute => new FlowAttributeView(attribute.Name, attribute.Value))],
            SourceBindings = SourceBindings(node),
        };

    private static FlowGroupView Group(
        NodeSpec node,
        NodePath path) =>
        new(
            path.Value,
            node.Label.Value,
            [.. (node.Nodes ?? []).Select(child => path.Append(child.Label).Value)])
        {
            ParentGroupPath = path.Parent?.Value,
            TypeName = node.Type?.Kind.Value,
            Inputs = [.. node.EffectiveInputs.Select(port => Input(port, node))],
            Outputs = [.. node.Outputs.Select(Output)],
            MaxExecutions = node.MaxExecutions ?? int.MaxValue,
        };

    private static FlowPortView Input(InputPort port, NodeSpec node) =>
        new(
            port.Name.Value,
            port.Type.Name,
            node.Bindings?.GetValueOrDefault(port.Name).Display ?? port.Default ?? port.Description,
            port.Trigger)
        {
            Static = port.IsStatic,
        };

    private static FlowPortView Output(Port port) =>
        new(
            port.Name.Value,
            port.Type.Name,
            port.Description ?? port.Value,
            port.Trigger)
        {
            Constant = port.IsConstant,
        };

    private static string? Model(NodeSpec node)
    {
        if (node.Type?.ModelPort is not { } modelPort)
        {
            return null;
        }

        if (node.Bindings?.GetValueOrDefault(modelPort) is { } reference)
        {
            return reference.Display;
        }

        return node.FindInput(modelPort)?.Default;
    }

    private static IReadOnlyList<string> SourceBindings(NodeSpec node) =>
    [
        .. node.From.Select(group => string.Join(
            '、',
            group.Links.Select(link => $"{link.Input.Value}←{link.Ref.Display}"))),
    ];

    private static FlowStartKind KindOf(StartInputKind kind) => kind switch
    {
        StartInputKind.Host => FlowStartKind.Host,
        StartInputKind.Fixed => FlowStartKind.Fixed,
        StartInputKind.Automatic => FlowStartKind.Automatic,
        _ => FlowStartKind.Content,
    };
}