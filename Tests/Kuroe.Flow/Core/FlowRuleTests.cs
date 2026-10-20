using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.StaticGraph;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>流程静态校验规则：直接构造模型，一条规则一个测试，断言完整错误说明集合。</summary>
public sealed class FlowRuleTests
{
    [Fact]
    public void Node_without_name_is_rejected() =>
        Assert.Equal(
            [NodeError("(未命名)", "节点名不能为空。")],
            Errors(Flow(Container("整体", Plan(string.Empty, "规划者")), Models("规划者"))));

    [Fact]
    public void Node_name_with_at_is_rejected() =>
        Assert.Equal(
            [NodeError("甲@乙", "节点名不能含 @，@ 是端口引用的分隔符。")],
            Errors(Flow(Container("整体", Text("甲@乙", "执行者")), Models("执行者"))));

    [Fact]
    public void Node_with_at_label_is_rejected() =>
        Assert.Equal(
            [NodeError("甲@乙", "节点标签不能含 @，@ 是端口引用的分隔符。")],
            Errors(Flow(Container("整体", Reference("甲@乙", "干活", "执行者")), Models("执行者"))));

    [Fact]
    public void Duplicate_node_label_is_rejected() =>
        Assert.Equal(
            [NodeError("制定计划", "同一容器内的节点标签重复。")],
            Errors(Flow(
                Container("整体", Plan("制定计划", "规划者"), Text("制定计划", "规划者")),
                Models("规划者"))));

    [Fact]
    public void Duplicate_model_name_is_rejected() =>
        Assert.Equal(
            [ModelError("规划者", "模型选择名重复。")],
            Errors(Flow(Container("整体", Plan("制定计划", "规划者")), Models("规划者", "规划者"))));

    [Fact]
    public void Unknown_model_reference_is_rejected() =>
        Assert.Equal(
            [NodeError("制定计划", "引用的模型选择 XXX 不存在。")],
            Errors(Flow(Container("整体", Plan("制定计划", "XXX")), Models("规划者"))));

    [Fact]
    public void Executable_max_executions_zero_is_rejected() =>
        Assert.Equal(
            [NodeError("干活", "MaxExecutions 必须是正整数。")],
            Errors(Flow(Container("整体", Exec("干活", "执行者", new ExecutableSpec { MaxExecutions = 0 })), Models("执行者"))));

    [Fact]
    public void Executable_max_executions_negative_is_rejected() =>
        Assert.Equal(
            [NodeError("干活", "MaxExecutions 必须是正整数。")],
            Errors(Flow(Container("整体", Exec("干活", "执行者", new ExecutableSpec { MaxExecutions = -1 })), Models("执行者"))));

    [Fact]
    public void Container_max_executions_zero_is_rejected() =>
        Assert.Equal(
            [NodeError("整体", "MaxExecutions 必须是正整数。")],
            Errors(Flow(Container("整体", Text("干活", "执行者")) with { MaxExecutions = 0 }, Models("执行者"))));

    [Fact]
    public void Container_max_executions_negative_is_rejected() =>
        Assert.Equal(
            [NodeError("整体", "MaxExecutions 必须是正整数。")],
            Errors(Flow(Container("整体", Text("干活", "执行者")) with { MaxExecutions = -2 }, Models("执行者"))));

    [Fact]
    public void Container_from_is_rejected() =>
        Assert.Equal(
            [NodeError("容器", "容器节点不是执行节点，不支持 From。")],
            Errors(Flow(
                Container("容器", Plan("规划", "规划者")) with { From = [Ref("规划", "结论")] },
                Models("规划者"))));

    [Fact]
    public void Container_from_with_signal_input_is_rejected() =>
        Assert.Equal(
            [NodeError("容器", "容器节点不是执行节点，不支持 From。")],
            Errors(Flow(
                Container("容器", Plan("规划", "规划者")) with { From = [Signal("规划", "结论")] },
                Models("规划者"))));

    [Fact]
    public void Start_without_output_port_is_rejected() =>
        Assert.Equal(
            ["流程 默认：流程必须声明开始入口，至少一个 Text 输出端口。"],
            Errors(new FlowDefinition(new FlowName("默认"), null, Models("执行者"),
                [], Container("整体", Exec("干活", "执行者", new ExecutableSpec()) with { From = [] }))));

    [Fact]
    public void Start_with_non_data_port_is_rejected() =>
        Assert.Equal(
            ["流程 默认：开始端口 目标 的类型只能是 Text。"],
            Errors(new FlowDefinition(new FlowName("默认"), null, Models("执行者"),
                [ContextPort("目标")], Container("整体", Exec("干活", "执行者", new ExecutableSpec()) with { From = [] }))));

    [Fact]
    public void Start_with_constant_port_is_accepted() =>
        Assert.Empty(Errors(StartFlow([StartOutput, P("背景") with { Value = "固定背景" }])));

    [Fact]
    public void Start_without_a_goal_port_is_rejected() =>
        Assert.Equal(
            ["流程 默认：开始入口必须恰好有一个不带 Value 的端口承接提交的任务目标。"],
            Errors(StartFlow([P("目标") with { Value = "固定" }])));

    [Fact]
    public void Start_with_two_goal_ports_is_rejected() =>
        Assert.Equal(
            ["流程 默认：开始入口必须恰好有一个不带 Value 的端口承接提交的任务目标。"],
            Errors(StartFlow([StartOutput, P("另一目标")])));

    [Fact]
    public void Start_with_blank_value_is_rejected() =>
        Assert.Equal(
            ["流程 默认：开始端口 背景 的 Value 不能为空。"],
            Errors(StartFlow([StartOutput, P("背景") with { Value = " " }])));

    [Fact]
    public void Start_with_duplicate_port_is_rejected() =>
        Assert.Equal(
            ["流程 默认：开始端口 背景 重复。"],
            Errors(StartFlow([StartOutput, P("背景") with { Value = "甲" }, P("背景") with { Value = "乙" }])));

    [Fact]
    public void Node_named_start_is_rejected() =>
        Assert.Equal(
            [NodeError("Start", "节点名 Start 是流程起点的保留名。")],
            Errors(Flow(Container("整体", Text("Start", "执行者")), Models("执行者"))));

    [Fact]
    public void Node_with_start_label_is_rejected() =>
        Assert.Equal(
            [NodeError("Start", "节点标签 Start 是流程起点的保留标签。")],
            Errors(Flow(Container("整体", Reference("Start", "干活", "执行者")), Models("执行者"))));

    [Fact]
    public void Node_unreachable_from_start_is_rejected() =>
        Assert.Equal(
            [
                MemberError("干活", "从开始不可达，不会被触发，请用 From 接到来源节点或 Start@目标。"),
                MemberError("收尾", "从开始不可达，不会被触发，请用 From 接到来源节点或 Start@目标。"),
            ],
            Errors(Flow(
                Container("整体",
                    Exec("干活", "执行者", new ExecutableSpec()) with { From = [] },
                    Exec("收尾", "执行者", new ExecutableSpec()) with { From = [] }),
                Models("执行者"))));

    [Fact]
    public void Two_context_ports_are_rejected() =>
        Assert.Equal(
            [NodeError("分析", "Context 输出端口至多声明一个。")],
            Errors(Flow(
                Container("整体", Text("分析", "执行者") with { Outputs = [ContextPort("甲"), ContextPort("乙")] }),
                Models("执行者"))));

    [Fact]
    public void Duplicate_output_port_is_rejected() =>
        Assert.Equal(
            [NodeError("分析", "输出端口 结论 重复。")],
            Errors(Flow(Container("整体", Text("分析", "执行者") with { Outputs = [P("结论"), P("结论")] }), Models("执行者"))));

    [Fact]
    public void Reference_to_missing_node_is_rejected() =>
        Assert.Equal(
            [MemberError("容器/内部", "From 引用的节点 不存在 不在流程里。")],
            Errors(Flow(
                Container("整体",
                    Plan("制定计划", "规划者"),
                    Container("容器", Text("内部", "执行者") with { From = [Ref("不存在", "结论")] })),
                Models("规划者", "执行者"))));

    [Fact]
    public void Signal_from_missing_node_is_rejected() =>
        Assert.Equal(
            [NodeError("触发", "From 引用的节点 不存在 不在流程里。")],
            Errors(Flow(Text("触发", "执行者") with { From = [Signal("不存在", "结论")] }, Models("执行者"))));

    [Fact]
    public void Reference_to_missing_port_is_rejected() =>
        Assert.Equal(
            [MemberError("实施", "节点 整体/分析 没有输出端口 不存在。")],
            Errors(Flow(
                Container("整体",
                    Text("分析", "执行者") with { Outputs = [P("结论")] },
                    Text("实施", "执行者") with { From = [Ref("分析", "不存在")] }),
                Models("执行者"))));

    [Fact]
    public void Context_input_from_container_is_rejected() =>
        Assert.Equal(
            [
                MemberError("实施", "容器 整体/箱子 没有输出端口 结论。"),
                MemberError("实施", "Context 输入来源 整体/箱子 必须是非输入执行节点。"),
            ],
            Errors(Flow(
                Container("整体",
                    Container("箱子", Text("干活", "执行者") with { Outputs = [P("结论")] }),
                    Text("实施", "执行者") with { Inputs = [In("结论", PortType.Context)], From = [Context("箱子", "结论")] }),
                Models("执行者"))));

    [Fact]
    public void Context_input_from_bound_port_is_rejected() =>
        Assert.Equal(
            [
                MemberError("实施", "From 连接 @输入 是对绑定端口的引用，请写明来源节点。"),
            ],
            Errors(Flow(Container("整体", Text("实施", "执行者") with { Inputs = [In("输入", PortType.Context)], From = [BoundContext("输入")] }), Models("执行者"))));

    [Fact]
    public void Duplicate_from_link_is_rejected() =>
        Assert.Equal(
            [NodeError("接收", "From 组内的目标输入端口 结论 出现多次。")],
            Errors(Flow(
                Container("整体",
                    Text("来源", "执行者") with { Outputs = [P("结论")] },
                    Text("接收", "执行者") with { From = [Twice("结论", "来源", "结论")] }),
                Models("执行者"))));

    /// <summary>组间来源端口不重复：同一来源端口出现在多个组里即接线重复。</summary>
    [Fact]
    public void Source_port_repeated_across_groups_is_rejected() =>
        Assert.Equal(
            [MemberError("汇合", "From 的来源端口 整体/甲@结论 出现在多个组里。")],
            Errors(Flow(
                Container("整体",
                    Text("甲", "执行者") with { Outputs = [P("结论")] },
                    Text("乙", "执行者") with { Outputs = [P("结论")] },
                    Text("汇合", "执行者") with
                    {
                        From = [Group(("甲", "结论")), Group(("甲", "结论"), ("乙", "结论"))],
                    }),
                Models("执行者"))));

    /// <summary>来源端口只按组划分归属：同一组内给不同输入名复用同一来源端口仍是合法接线。</summary>
    [Fact]
    public void Source_port_reused_within_one_group_is_accepted() =>
        Assert.Empty(Errors(Flow(
            Container("整体",
                Text("来源", "执行者") with { Outputs = [P("结论")] },
                Text("接收", "执行者") with
                {
                    Inputs = [In("甲"), In("乙")],
                    From =
                    [
                        new([
                            new FromLink(new PortName("甲"), PortRef.Named(new NodeLabel("来源"), new PortName("结论"))),
                            new FromLink(new PortName("乙"), PortRef.Named(new NodeLabel("来源"), new PortName("结论"))),
                        ]),
                    ],
                }),
            Models("执行者"))));

    [Fact]
    public void Signal_driver_is_accepted() =>
        Assert.Empty(Errors(Flow(
            Container("整体",
                Text("放行", "执行者") with { Outputs = [P("结论")] },
                Exec("实施", "执行者", new ExecutableSpec()) with
                {
                    Inputs = [In("结论"), In("批准", PortType.Signal)],
                    From =
                    [
                        new([
                            new FromLink(new PortName("结论"), PortRef.Named(new NodeLabel("放行"), new PortName("结论"))),
                            new FromLink(new PortName("批准"), PortRef.Named(new NodeLabel("放行"), new PortName("结论"))),
                        ]),
                    ],
                }),
            Models("执行者"))));

    [Fact]
    public void Container_out_bound_to_sub_container_is_rejected() =>
        Assert.Equal(
            [NodeError("整体/交付", "容器输出端口 结果 的绑定目标 整体/交付/撰写组 不是执行节点，无法取产出。")],
            Errors(Flow(
                Container("整体",
                    Container("交付", Container("撰写组", Text("撰写", "执行者") with { Outputs = [P("结论")] })) with
                    {
                        ContainerOutputs = new Dictionary<PortName, ContainerOutput> { [PN("结果")] = new(PortRef.Named(new NodeLabel("撰写组"), PN("结论"))) },
                    }),
                Models("执行者"))));

    [Fact]
    public void Reference_cycle_without_source_is_rejected() =>
        Assert.Equal(
            [
                MemberError("乙", "引用环没有环外来源且环内没有输入节点，任务无法启动。"),
                MemberError("甲", "从开始不可达，不会被触发，请用 From 接到来源节点或 Start@目标。"),
                MemberError("乙", "从开始不可达，不会被触发，请用 From 接到来源节点或 Start@目标。"),
            ],
            Errors(Flow(
                Container("整体",
                    Text("甲", "执行者") with { Outputs = [P("结论")], From = [Ref("乙", "结论")] },
                    Text("乙", "执行者") with { Outputs = [P("结论")], From = [Ref("甲", "结论")] }),
                Models("执行者"))));

    [Fact]
    public void Executable_referencing_its_own_container_is_rejected() =>
        Assert.Equal(
            [
                MemberError("交付/撰写", "执行节点不能从自身所在容器取输入，容器会永远等不到齐备。"),
                MemberError("交付/撰写", "引用环没有环外来源且环内没有输入节点，任务无法启动。"),
                MemberError("交付/撰写", "从开始不可达，不会被触发，请用 From 接到来源节点或 Start@目标。"),
            ],
            Errors(Flow(
                Container("整体",
                    Container("交付", Text("撰写", "执行者") with { Outputs = [P("结论")], From = [Ref("交付", "结论")] }) with
                    {
                        ContainerOutputs = new Dictionary<PortName, ContainerOutput> { [PN("结论")] = new(PortRef.Named(new NodeLabel("撰写"), PN("结论"))) },
                    }),
                Models("执行者"))));

    [Fact]
    public void Executable_referencing_its_own_container_with_sibling_is_rejected() =>
        Assert.Equal(
            [
                MemberError("交付/撰写", "执行节点不能从自身所在容器取输入，容器会永远等不到齐备。"),
                MemberError("交付/撰写", "从开始不可达，不会被触发，请用 From 接到来源节点或 Start@目标。"),
            ],
            Errors(Flow(
                Container("整体",
                    Container("交付",
                        Text("撰写", "执行者") with { Outputs = [P("结论")], From = [Ref("交付", "结论")] },
                        Text("排版", "执行者")) with
                    {
                        ContainerOutputs = new Dictionary<PortName, ContainerOutput> { [PN("结论")] = new(PortRef.Named(new NodeLabel("撰写"), PN("结论"))) },
                    }),
                Models("执行者"))));

    [Fact]
    public void Container_cycle_across_scopes_is_rejected() =>
        Assert.Equal(
            [
                NodeError("整体/容器乙/乙", "引用环没有环外来源且环内没有输入节点，任务无法启动。"),
                NodeError("整体/容器甲/甲", "从开始不可达，不会被触发，请用 From 接到来源节点或 Start@目标。"),
                NodeError("整体/容器乙/乙", "从开始不可达，不会被触发，请用 From 接到来源节点或 Start@目标。"),
            ],
            Errors(Flow(
                Container("整体",
                    Container("容器甲", Text("甲", "执行者") with { Outputs = [P("结论")], From = [Ref("容器乙", "结论")] }) with
                    {
                        ContainerOutputs = new Dictionary<PortName, ContainerOutput> { [PN("结论")] = new(PortRef.Named(new NodeLabel("甲"), PN("结论"))) },
                    },
                    Container("容器乙", Text("乙", "执行者") with { Outputs = [P("结论")], From = [Ref("容器甲", "结论")] }) with
                    {
                        ContainerOutputs = new Dictionary<PortName, ContainerOutput> { [PN("结论")] = new(PortRef.Named(new NodeLabel("乙"), PN("结论"))) },
                    }),
                Models("执行者"))));

    /// <summary>每套绑定方案都必须覆盖全部输入端口：两套方案任一齐备即启动，规则接受这种写法。</summary>
    [Fact]
    public void Alternative_groups_are_accepted() =>
        Assert.Empty(Errors(Flow(
            Container("整体",
                Text("准备甲", "执行者") with { Outputs = [P("结论")] },
                Text("准备乙", "执行者") with { Outputs = [P("结论")] },
                Exec("实施", "执行者", new ExecutableSpec()) with
                {
                    Inputs = [In("结论"), In("依据")],
                    Outputs = [P("结果")],
                    From =
                    [
                        new([
                            new FromLink(new PortName("结论"), PortRef.Named(new NodeLabel("准备甲"), new PortName("结论"))),
                            new FromLink(new PortName("依据"), PortRef.Named(new NodeLabel("准备甲"), new PortName("结论"))),
                        ]),
                        new([
                            new FromLink(new PortName("结论"), PortRef.Named(new NodeLabel("准备乙"), new PortName("结论"))),
                            new FromLink(new PortName("依据"), PortRef.Named(new NodeLabel("准备乙"), new PortName("结论"))),
                        ]),
                    ],
                }),
            Models("执行者"))));

    /// <summary>输入端口名在同一节点内唯一，与输出端口同一份规则。</summary>
    [Fact]
    public void Duplicate_input_port_is_rejected() =>
        Assert.Equal(
            [NodeError("接收", "输入端口 结论 重复。")],
            Errors(Flow(
                Container("整体",
                    Text("来源", "执行者") with { Outputs = [P("结论")] },
                    Text("接收", "执行者") with { Inputs = [In("结论"), In("结论")], From = [Ref("来源", "结论")] }),
                Models("执行者"))));

    /// <summary>容器定义条目。</summary>
    private static NodeSpec Container(string name, params NodeSpec[] members) =>
        NodeSpec.Define(new NodeName(name)) with { Type = ContainerNodeTypes.Type, Nodes = [.. members] };

    /// <summary>执行节点定义条目，默认由开始入口触发。</summary>
    private static NodeSpec Text(string name, string model) =>
        Exec(name, model, new ExecutableSpec());

    /// <summary>执行节点定义条目，带完整执行配置，默认由开始入口触发。</summary>
    private static NodeSpec Exec(string name, string model, ExecutableSpec spec) =>
        NodeSpec.Define(new NodeName(name)) with
        {
            Type = TestNodeTypes.Executable,
            Model = new ModelRef(model),
            Spec = spec,
            From = [StartRef()],
        };

    /// <summary>被使用的节点，标签是它在图里的标识段，类型与执行配置取展开后的形态。</summary>
    private static NodeSpec Reference(string label, string use, string model) =>
        NodeSpec.Reference(new NodeLabel(label), new NodeName(use)) with
        {
            Type = TestNodeTypes.Executable,
            Model = new ModelRef(model),
            Spec = new ExecutableSpec(),
            From = [StartRef()],
        };

    /// <summary>承接目标的计划节点，声明一个计划输出端口。</summary>
    private static NodeSpec Plan(string name, string model) =>
        Exec(name, model, new ExecutableSpec()) with { Outputs = [P("计划")] };

    /// <summary>Text 端口声明。</summary>
    private static Port P(string name) => new(new PortName(name), PortType.Text);

    /// <summary>Context 端口声明。</summary>
    private static Port ContextPort(string name) => new(new PortName(name), PortType.Context);

    /// <summary>输入端口声明，未写类型时按 Text。</summary>
    private static InputPort In(string name, PortType type = PortType.Text) =>
        new(new PortName(name), type);

    /// <summary>命名端口的引用名。</summary>
    private static PortName PN(string name) => new(name);

    /// <summary>单来源启动组，输入名取来源端口名。</summary>
    private static FromGroup Ref(string source, string port) => One(port, source, port);

    /// <summary>信号输入的启动组，目标输入端口须声明 Signal 类型。</summary>
    private static FromGroup Signal(string source, string port) => One(port, source, port);

    /// <summary>上下文输入的启动组，目标输入端口须声明 Context 类型。</summary>
    private static FromGroup Context(string source, string port) => One(port, source, port);

    /// <summary>引用绑定端口的上下文输入。</summary>
    private static FromGroup BoundContext(string port) =>
        new([new FromLink(new PortName(port), PortRef.Bound(new PortName(port)))]);


    /// <summary>一条连接构成的启动组。</summary>
    private static FromGroup One(string input, string source, string port) =>
        new([new FromLink(new PortName(input), PortRef.Named(new NodeLabel(source), new PortName(port)))]);

    /// <summary>同一输入名两条连接的启动组，用于组内重复判定。</summary>
    private static FromGroup Twice(string input, string source, string port) =>
        new([
            new FromLink(new PortName(input), PortRef.Named(new NodeLabel(source), new PortName(port))),
            new FromLink(new PortName(input), PortRef.Named(new NodeLabel(source), new PortName(port))),
        ]);

    /// <summary>一组启动条件，输入名取来源名。</summary>
    private static FromGroup Group(params (string Source, string Port)[] links) =>
        new([.. links.Select(link => new FromLink(
            new PortName(link.Source),
            PortRef.Named(new NodeLabel(link.Source), new PortName(link.Port))))]);

    /// <summary>开始入口的输出端口名。</summary>
    private static PortName StartPort => new("目标");

    /// <summary>开始入口的输出端口声明。</summary>
    private static Port StartOutput => new(StartPort, PortType.Text);

    /// <summary>由开始入口触发的启动组。</summary>
    private static FromGroup StartRef() => One("目标", StartNode.ReservedLabel.Value, "目标");

    /// <summary>流程的模型选择表，只声明名字。</summary>
    private static ModelDefinition[] Models(params string[] names) =>
        [.. names.Select(name => new ModelDefinition { Name = new ModelRef(name) })];

    /// <summary>一条名为默认的流程。</summary>
    private static FlowDefinition Flow(NodeSpec root, params ModelDefinition[] models) =>
        new(new FlowName("默认"), null, models, [StartOutput], root);

    /// <summary>一条名为默认的流程，开始端口按给定条目声明，根容器内一个执行节点取开始的目标端口。</summary>
    private static FlowDefinition StartFlow(IReadOnlyList<Port> startOutputs) =>
        new(new FlowName("默认"), null, Models("执行者"), startOutputs,
            Container("整体", Exec("干活", "执行者", new ExecutableSpec())));

    /// <summary>流程内某个节点的错误说明。</summary>
    private static string NodeError(string node, string message) =>
        $"流程 默认 的节点 {node}：{message}";

    /// <summary>根容器 整体 的直接成员的错误说明，节点按树路径展示。</summary>
    private static string MemberError(string node, string message) =>
        NodeError($"整体/{node}", message);

    /// <summary>流程内某个模型选择的错误说明。</summary>
    private static string ModelError(string model, string message) =>
        $"流程 默认 的模型选择 {model}：{message}";

    /// <summary>校验流程，取错误说明。类型规则按引擎内置类型注册表判定。</summary>
    private static IReadOnlyList<string> Errors(FlowDefinition flow) =>
        [.. FlowRules.Validate(flow).ErrorsOrEmptyList.Select(error => error.Description)];
}
