using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Workflows.FlowAssembly;
using Xunit;

namespace Kuroe.Tests;

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
    public void Duplicate_node_name_is_rejected() =>
        Assert.Equal(
            [NodeError("制定计划", "节点名重复。")],
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
    public void Executable_max_runs_zero_is_rejected() =>
        Assert.Equal(
            [NodeError("干活", "MaxRuns 必须是正整数。")],
            Errors(Flow(Container("整体", Exec("干活", "执行者", new ExecutableSpec { MaxRuns = 0 })), Models("执行者"))));

    [Fact]
    public void Executable_max_runs_negative_is_rejected() =>
        Assert.Equal(
            [NodeError("干活", "MaxRuns 必须是正整数。")],
            Errors(Flow(Container("整体", Exec("干活", "执行者", new ExecutableSpec { MaxRuns = -1 })), Models("执行者"))));

    [Fact]
    public void Container_max_runs_zero_is_rejected() =>
        Assert.Equal(
            [NodeError("整体", "MaxRuns 必须是正整数。")],
            Errors(Flow(Container("整体", Text("干活", "执行者")) with { MaxRuns = 0 }, Models("执行者"))));

    [Fact]
    public void Container_max_runs_negative_is_rejected() =>
        Assert.Equal(
            [NodeError("整体", "MaxRuns 必须是正整数。")],
            Errors(Flow(Container("整体", Text("干活", "执行者")) with { MaxRuns = -2 }, Models("执行者"))));

    [Fact]
    public void Container_from_is_rejected() =>
        Assert.Equal(
            [NodeError("容器", "容器节点不是执行节点，不支持 From。")],
            Errors(Flow(
                Container("容器", Plan("规划", "规划者")) with { From = [Ref("规划", "结论")] },
                Models("规划者"))));

    [Fact]
    public void Container_signal_entry_is_rejected_as_from() =>
        Assert.Equal(
            [NodeError("容器", "容器节点不是执行节点，不支持 From。")],
            Errors(Flow(
                Container("容器", Plan("规划", "规划者")) with { From = [Signal("规划", "结论")] },
                Models("规划者"))));

    [Fact]
    public void Split_on_non_plan_executable_is_rejected() =>
        Assert.Equal(
            [NodeError("实施", "Split 只能写在规划节点上。")],
            Errors(Flow(Container("整体", Exec("实施", "执行者", new ExecutableSpec { Split = StaticSplit() })), Models("执行者"))));

    [Fact]
    public void Empty_split_is_rejected() =>
        Assert.Equal(
            [NodeError("制定计划", "Split 至少要声明 Items 或 ExtrasMax。")],
            Errors(Flow(
                Container("整体", Exec("制定计划", "执行者", new ExecutableSpec { Output = NodeOutput.Plan, Split = new SplitConfig(null, null, null) })),
                Models("执行者"))));

    [Fact]
    public void Split_extras_over_cap_is_rejected() =>
        Assert.Equal(
            [NodeError("制定计划", "Split.ExtrasMax 必须是 0 到 20 的整数。")],
            Errors(Flow(
                Container("整体", Exec("制定计划", "执行者", new ExecutableSpec { Output = NodeOutput.Plan, Split = new SplitConfig(null, 21, null) })),
                Models("执行者"))));

    [Fact]
    public void Split_zero_without_items_is_rejected() =>
        Assert.Equal(
            [NodeError("制定计划", "Split.ExtrasMax 为 0 时要求声明至少一条 Items。")],
            Errors(Flow(
                Container("整体", Exec("制定计划", "执行者", new ExecutableSpec { Output = NodeOutput.Plan, Split = new SplitConfig(null, 0, null) })),
                Models("执行者"))));

    [Fact]
    public void Static_split_with_prompt_is_rejected() =>
        Assert.Equal(
            [NodeError("制定计划", "纯静态拆分节点不支持 Prompt。")],
            Errors(Flow(
                Container("整体", Exec("制定计划", "执行者", new ExecutableSpec { Output = NodeOutput.Plan, Prompt = "不需要", Split = StaticSplit() })),
                Models("执行者"))));

    [Fact]
    public void Static_split_with_gate_is_rejected() =>
        Assert.Equal(
            [NodeError("制定计划", "纯静态拆分节点不支持待批准门控。")],
            Errors(Flow(
                Container("整体", Exec("制定计划", "执行者", new ExecutableSpec { Output = NodeOutput.Plan, Split = StaticSplit() }) with { Gate = NodeGate.Review }),
                Models("执行者"))));

    [Fact]
    public void Static_split_with_from_is_rejected() =>
        Assert.Equal(
            [NodeError("制定计划", "纯静态拆分节点不支持 From。")],
            Errors(Flow(
                Container("整体",
                    Text("上游", "执行者") with { Outputs = [P("结论")] },
                    Exec("制定计划", "执行者", new ExecutableSpec { Output = NodeOutput.Plan, Split = StaticSplit() }) with { From = [Ref("上游", "结论")] }),
                Models("执行者"))));

    [Fact]
    public void Static_split_with_signal_entry_is_rejected() =>
        Assert.Equal(
            [NodeError("制定计划", "纯静态拆分节点不支持 From。")],
            Errors(Flow(
                Container("整体",
                    Text("来源", "执行者") with { Outputs = [P("结论")] },
                    Exec("制定计划", "执行者", new ExecutableSpec { Output = NodeOutput.Plan, Split = StaticSplit() }) with { From = [Signal("来源", "结论")] }),
                Models("执行者"))));

    [Fact]
    public void Split_item_branch_without_executable_is_rejected() =>
        Assert.Equal(
            [NodeError("制定计划", "Split.Items 的分支“不存在”没有对应的分支执行节点。")],
            Errors(Flow(
                Container("整体",
                    Exec("制定计划", "执行者", new ExecutableSpec { Output = NodeOutput.Plan, Split = new SplitConfig([new SplitItem("甲", "做甲", string.Empty, new BranchName("不存在"))], null, null) }),
                    Exec("撰写", "执行者", PerItem()) with { From = [Ref("制定计划", "拆分")] }),
                Models("执行者"))));

    [Fact]
    public void Input_with_tools_is_rejected() =>
        Assert.Equal(
            [NodeError("收集", "输入节点不启动 run，不能声明 Tools。")],
            Errors(Flow(Container("整体", Input("收集") with { Execution = new ExecutableSpec { Output = NodeOutput.Input, Tools = [new ToolPath("GetLocalTime")] } }))));

    [Fact]
    public void Input_with_prompt_is_rejected() =>
        Assert.Equal(
            [NodeError("收集", "输入节点不启动 run，不能声明 Prompt。")],
            Errors(Flow(Container("整体", Input("收集") with { Execution = new ExecutableSpec { Output = NodeOutput.Input, Prompt = "多想想" } }))));

    [Fact]
    public void Input_with_model_is_rejected() =>
        Assert.Equal(
            [NodeError("收集", "输入节点不启动 run，不能声明模型选择。")],
            Errors(Flow(Container("整体", Input("收集") with { Model = new ModelRef("执行者") }))));

    [Fact]
    public void Input_with_per_item_is_rejected() =>
        Assert.Equal(
            [NodeError("收集", "输入节点只能整节点等待用户输入。")],
            Errors(Flow(Container("整体", Input("收集") with { Execution = new ExecutableSpec { Output = NodeOutput.Input, Mode = NodeMode.PerItem } }))));

    [Fact]
    public void Input_with_branch_is_rejected() =>
        Assert.Equal(
            [NodeError("收集", "输入节点不启动 run，不能声明 Branch。")],
            Errors(Flow(Container("整体", Input("收集") with { Execution = new ExecutableSpec { Output = NodeOutput.Input, Branch = new BranchName("甲") } }))));

    [Fact]
    public void Input_with_validate_is_rejected() =>
        Assert.Equal(
            [NodeError("收集", "输入节点不启动 run，不能声明 Validate。")],
            Errors(Flow(Container("整体", Input("收集") with { Outputs = [P("回答")], Execution = new ExecutableSpec { Output = NodeOutput.Input, Validate = new OutputValidation(ValidationPredicate.NonEmpty, null) } }))));

    [Fact]
    public void Input_with_signal_entry_is_rejected() =>
        Assert.Equal(
            [NodeError("收集", "输入节点不启动 run，不能声明触发信号。")],
            Errors(Flow(
                Container("整体",
                    Text("准备", "执行者") with { Outputs = [P("结论")] },
                    Input("收集") with { From = [Signal("准备", "结论")] }),
                Models("执行者"))));

    [Fact]
    public void Input_with_group_entry_is_rejected() =>
        Assert.Equal(
            [NodeError("收集", "输入节点不启动 run，不能声明可选启动组。")],
            Errors(Flow(
                Container("整体",
                    Text("准备", "执行者") with { Outputs = [P("结论")] },
                    Input("收集") with { From = [InGroup("准备", "结论", "任选")] }),
                Models("执行者"))));

    [Fact]
    public void Input_with_review_gate_is_rejected() =>
        Assert.Equal(
            [NodeError("收集", "输入节点回答即放行，不能声明 Review 门控。")],
            Errors(Flow(Container("整体", Input("收集") with { Gate = NodeGate.Review }))));

    [Fact]
    public void Input_with_system_prompt_is_rejected() =>
        Assert.Equal(
            [NodeError("收集", "输入节点不启动 run，不能声明系统指令。")],
            Errors(Flow(Container("整体", Input("收集") with { SystemPrompt = ["背景"] }))));

    [Fact]
    public void Input_with_two_outputs_is_rejected() =>
        Assert.Equal(
            [NodeError("收集", "输入节点最多声明一个输出端口，回答写入该端口。")],
            Errors(Flow(Container("整体", Input("收集") with { Outputs = [P("结论"), P("理由")] }))));

    [Fact]
    public void Input_with_split_is_rejected() =>
        Assert.Equal(
            [NodeError("收集", "Split 只能写在规划节点上。")],
            Errors(Flow(Container("整体", Input("收集") with { Execution = new ExecutableSpec { Output = NodeOutput.Input, Split = StaticSplit() } }))));

    [Fact]
    public void Question_on_text_executable_is_rejected() =>
        Assert.Equal(
            [NodeError("干活", "Question 只属于输入节点。")],
            Errors(Flow(Container("整体", Exec("干活", "执行者", new ExecutableSpec { Question = "你想问什么" })), Models("执行者"))));

    [Fact]
    public void Outputs_on_plan_executable_is_rejected() =>
        Assert.Equal(
            [NodeError("分析", "输出端口只能声明在文本执行节点上。")],
            Errors(Flow(Container("整体", Plan("分析", "执行者") with { Outputs = [P("结论")] }), Models("执行者"))));

    [Fact]
    public void Reserved_split_port_name_is_rejected() =>
        Assert.Equal(
            [NodeError("分析", "输出端口名 拆分 是保留名，不可作命名输出端口声明。")],
            Errors(Flow(Container("整体", Text("分析", "执行者") with { Outputs = [P("拆分")] }), Models("执行者"))));

    [Fact]
    public void Reserved_context_output_port_name_is_rejected() =>
        Assert.Equal(
            [NodeError("分析", "输出端口名 ContextOutput 是保留名，不可作命名输出端口声明。")],
            Errors(Flow(Container("整体", Text("分析", "执行者") with { Outputs = [P("ContextOutput")] }), Models("执行者"))));

    [Fact]
    public void Reserved_context_input_port_name_is_rejected() =>
        Assert.Equal(
            [NodeError("分析", "输出端口名 ContextInput 是保留名，不可作命名输出端口声明。")],
            Errors(Flow(Container("整体", Text("分析", "执行者") with { Outputs = [P("ContextInput")] }), Models("执行者"))));

    [Fact]
    public void Duplicate_output_port_is_rejected() =>
        Assert.Equal(
            [NodeError("分析", "输出端口 结论 重复。")],
            Errors(Flow(Container("整体", Text("分析", "执行者") with { Outputs = [P("结论"), P("结论")] }), Models("执行者"))));

    [Fact]
    public void Reference_to_missing_node_is_rejected() =>
        Assert.Equal(
            [NodeError("内部", "From 引用的节点 不存在 不在流程里。")],
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
            [NodeError("实施", "节点 分析 没有输出端口 不存在。")],
            Errors(Flow(
                Container("整体",
                    Text("分析", "执行者") with { Outputs = [P("结论")] },
                    Text("实施", "执行者") with { From = [Ref("分析", "不存在")] }),
                Models("执行者"))));

    [Fact]
    public void Context_port_from_input_node_is_rejected() =>
        Assert.Equal(
            [NodeError("实施", "输入节点不启动 run，不能作为上下文端口来源。")],
            Errors(Flow(
                Container("整体",
                    Input("用户输入"),
                    Text("实施", "执行者") with { From = [Ref("用户输入", "ContextOutput")] }),
                Models("执行者"))));

    [Fact]
    public void Context_entry_from_container_is_rejected() =>
        Assert.Equal(
            [
                NodeError("实施", "Context 条目来源 箱子 必须是非输入执行节点。"),
                NodeError("实施", "容器 箱子 没有输出端口 结论。"),
            ],
            Errors(Flow(
                Container("整体",
                    Container("箱子", Text("干活", "执行者") with { Outputs = [P("结论")] }),
                    Text("实施", "执行者") with { From = [Context("箱子", "结论")] }),
                Models("执行者"))));

    [Fact]
    public void Context_entry_from_bound_port_is_rejected() =>
        Assert.Equal(
            [
                NodeError("实施", "Context 条目来源必须写节点名或 来源@端口，不能引用绑定端口。"),
                NodeError("实施", "From 引用的节点 @输入 不在流程里。"),
            ],
            Errors(Flow(Container("整体", Text("实施", "执行者") with { From = [BoundContext("输入")] }), Models("执行者"))));

    [Fact]
    public void Context_entry_from_input_node_is_rejected() =>
        Assert.Equal(
            [NodeError("实施", "Context 条目来源 用户输入 不能是输入节点。")],
            Errors(Flow(
                Container("整体",
                    Input("用户输入") with { Outputs = [P("回答")] },
                    Text("实施", "执行者") with { From = [Context("用户输入", "回答")] }),
                Models("执行者"))));

    [Fact]
    public void Context_entry_from_per_item_node_is_rejected() =>
        Assert.Equal(
            [NodeError("汇报", "Context 条目来源 实施 不能是按条目展开的节点。")],
            Errors(Flow(
                Container("整体",
                    Plan("制定", "执行者"),
                    Exec("实施", "执行者", PerItem()) with { Outputs = [P("结论")], From = [Ref("制定", "拆分")] },
                    Text("汇报", "执行者") with { From = [Context("实施", "结论")] }),
                Models("执行者"))));

    [Fact]
    public void Duplicate_from_entry_is_rejected() =>
        Assert.Equal(
            [NodeError("接收", "From 引用的节点 来源@结论 出现多次。")],
            Errors(Flow(
                Container("整体",
                    Text("来源", "执行者") with { Outputs = [P("结论")] },
                    Text("接收", "执行者") with { From = [Ref("来源", "结论"), Ref("来源", "结论")] }),
                Models("执行者"))));

    [Fact]
    public void Per_item_with_signal_entry_is_rejected() =>
        Assert.Equal(
            [NodeError("实施", "声明可选启动组、触发信号或上下文输入的执行节点不能按条目展开，必须是整节点执行。")],
            Errors(Flow(
                Container("整体",
                    Text("来源", "执行者") with { Outputs = [P("结论")] },
                    Exec("实施", "执行者", PerItem()) with { From = [Signal("来源", "结论")] }),
                Models("执行者"))));

    [Fact]
    public void Container_out_bound_to_sub_container_is_rejected() =>
        Assert.Equal(
            [NodeError("交付", "容器输出端口 结果 的绑定目标节点 撰写组 不是执行节点，无法取产出。")],
            Errors(Flow(
                Container("整体",
                    Container("交付", Container("撰写组", Text("撰写", "执行者") with { Outputs = [P("结论")] })) with
                    {
                        Out = new Dictionary<PortName, PortRef> { [P("结果")] = PortRef.Context(new NodeName("撰写组")) },
                    }),
                Models("执行者"))));

    [Fact]
    public void Container_out_bound_to_input_node_is_rejected() =>
        Assert.Equal(
            [NodeError("交付", "容器输出端口 结果 的绑定目标节点 收集 是输入节点，不能作为上下文端口来源。")],
            Errors(Flow(
                Container("整体",
                    Container("交付", Input("收集") with { Outputs = [P("回答")] }) with
                    {
                        Out = new Dictionary<PortName, PortRef> { [P("结果")] = PortRef.Context(new NodeName("收集")) },
                    }),
                Models("执行者"))));

    [Fact]
    public void Per_item_executable_without_source_is_rejected() =>
        Assert.Equal(
            [NodeError("实施", "按条目展开的执行节点必须从规划执行节点或其它展开执行节点取输入。")],
            Errors(Flow(
                Container("整体",
                    Exec("实施", "执行者", PerItem()) with { Outputs = [P("结论")] },
                    Plan("制定计划", "规划者"),
                    Exec("收尾", "执行者", PerItem()) with { From = [Ref("实施", "结论")] }),
                Models("执行者", "规划者"))));

    [Fact]
    public void Per_item_from_non_plan_is_rejected() =>
        Assert.Equal(
            [NodeError("实施", "按条目展开的执行节点只能从规划执行节点取拆分。")],
            Errors(Flow(
                Container("整体",
                    Text("准备", "执行者") with { Outputs = [P("结论")] },
                    Exec("实施", "执行者", PerItem()) with { Outputs = [P("结论")], From = [Ref("准备", "拆分")] },
                    Exec("收尾", "执行者", PerItem()) with { From = [Ref("实施", "结论")] }),
                Models("执行者"))));

    [Fact]
    public void Branch_on_single_executable_is_rejected() =>
        Assert.Equal(
            [
                NodeError("撰写", "拆分端口只能被按条目展开的执行节点消费。"),
                NodeError("撰写", "声明分支 撰写 的执行节点必须按条目展开并从规划执行节点取拆分。"),
            ],
            Errors(Flow(
                Container("整体",
                    Plan("制定计划", "规划者"),
                    Exec("撰写", "执行者", new ExecutableSpec { Branch = new BranchName("撰写") }) with { From = [Ref("制定计划", "拆分")] }),
                Models("规划者", "执行者"))));

    [Fact]
    public void Reference_cycle_without_source_is_rejected() =>
        Assert.Equal(
            [NodeError("乙", "引用环没有环外来源且环内没有输入节点，任务无法启动。")],
            Errors(Flow(
                Container("整体",
                    Text("甲", "执行者") with { Outputs = [P("结论")], From = [Ref("乙", "结论")] },
                    Text("乙", "执行者") with { Outputs = [P("结论")], From = [Ref("甲", "结论")] }),
                Models("执行者"))));

    [Fact]
    public void Aligned_references_across_splits_are_rejected() =>
        Assert.Equal(
            [NodeError("收尾", "逐条对齐的两端必须来自同一个拆分。")],
            Errors(Flow(
                Container("整体",
                    Plan("制定A计划", "规划者"),
                    Plan("制定B计划", "规划者"),
                    Exec("实施A", "执行者", PerItem()) with { Outputs = [P("结论")], From = [Ref("制定A计划", "拆分")] },
                    Exec("实施B", "执行者", PerItem()) with { Outputs = [P("结论")], From = [Ref("制定B计划", "拆分")] },
                    Exec("收尾", "执行者", PerItem()) with { From = [Ref("实施A", "结论"), Ref("实施B", "结论")] }),
                Models("规划者", "执行者"))));

    [Fact]
    public void Executable_referencing_its_own_container_is_rejected() =>
        Assert.Equal(
            [
                NodeError("撰写", "执行节点不能从自身所在容器取输入，容器会永远等不到齐备。"),
                NodeError("撰写", "引用环没有环外来源且环内没有输入节点，任务无法启动。"),
            ],
            Errors(Flow(
                Container("整体",
                    Container("交付", Text("撰写", "执行者") with { Outputs = [P("结论")], From = [Ref("交付", "结论")] }) with
                    {
                        Out = new Dictionary<PortName, PortRef> { [P("结论")] = PortRef.Named(new NodeName("撰写"), P("结论")) },
                    }),
                Models("执行者"))));

    [Fact]
    public void Executable_referencing_its_own_container_with_sibling_is_rejected() =>
        Assert.Equal(
            [NodeError("撰写", "执行节点不能从自身所在容器取输入，容器会永远等不到齐备。")],
            Errors(Flow(
                Container("整体",
                    Container("交付",
                        Text("撰写", "执行者") with { Outputs = [P("结论")], From = [Ref("交付", "结论")] },
                        Text("排版", "执行者")) with
                    {
                        Out = new Dictionary<PortName, PortRef> { [P("结论")] = PortRef.Named(new NodeName("撰写"), P("结论")) },
                    }),
                Models("执行者"))));

    [Fact]
    public void Container_cycle_across_scopes_is_rejected() =>
        Assert.Equal(
            [NodeError("乙", "引用环没有环外来源且环内没有输入节点，任务无法启动。")],
            Errors(Flow(
                Container("整体",
                    Container("容器甲", Text("甲", "执行者") with { Outputs = [P("结论")], From = [Ref("容器乙", "结论")] }) with
                    {
                        Out = new Dictionary<PortName, PortRef> { [P("结论")] = PortRef.Named(new NodeName("甲"), P("结论")) },
                    },
                    Container("容器乙", Text("乙", "执行者") with { Outputs = [P("结论")], From = [Ref("容器甲", "结论")] }) with
                    {
                        Out = new Dictionary<PortName, PortRef> { [P("结论")] = PortRef.Named(new NodeName("乙"), P("结论")) },
                    }),
                Models("执行者"))));

    /// <summary>容器。</summary>
    private static NodeSpec Container(string name, params NodeSpec[] members) =>
        new() { Name = new NodeName(name), Nodes = [.. members] };

    /// <summary>文本执行节点。</summary>
    private static NodeSpec Text(string name, string model) =>
        new() { Name = new NodeName(name), Model = new ModelRef(model), Execution = new ExecutableSpec() };

    /// <summary>执行节点，带完整执行配置。</summary>
    private static NodeSpec Exec(string name, string model, ExecutableSpec execution) =>
        new() { Name = new NodeName(name), Model = new ModelRef(model), Execution = execution };

    /// <summary>交回规划的拆分源。</summary>
    private static NodeSpec Plan(string name, string model) =>
        Exec(name, model, new ExecutableSpec { Output = NodeOutput.Plan });

    /// <summary>输入节点。</summary>
    private static NodeSpec Input(string name) =>
        new() { Name = new NodeName(name), Execution = new ExecutableSpec { Output = NodeOutput.Input } };

    /// <summary>命名端口。</summary>
    private static PortName P(string name) => new(name);

    /// <summary>按条目展开的执行配置。</summary>
    private static ExecutableSpec PerItem() => new() { Mode = NodeMode.PerItem };

    /// <summary>命名端口的接线。</summary>
    private static SourceRef Ref(string source, string port) =>
        new(PortRef.Named(new NodeName(source), new PortName(port)));

    /// <summary>触发信号的接线。</summary>
    private static SourceRef Signal(string source, string port) =>
        new(PortRef.Named(new NodeName(source), new PortName(port)), Signal: true);

    /// <summary>上下文输入的接线。</summary>
    private static SourceRef Context(string source, string port) =>
        new(PortRef.Named(new NodeName(source), new PortName(port)), Context: true);

    /// <summary>引用绑定端口的上下文接线。</summary>
    private static SourceRef BoundContext(string port) =>
        new(PortRef.Bound(new PortName(port)), Context: true);

    /// <summary>可选启动组的接线。</summary>
    private static SourceRef InGroup(string source, string port, string group) =>
        new(PortRef.Named(new NodeName(source), new PortName(port)), Or: group);

    /// <summary>只有静态条目的拆分配置。</summary>
    private static SplitConfig StaticSplit() =>
        new([new SplitItem("甲", "做甲", string.Empty, null)], null, null);

    /// <summary>流程的模型选择表，只声明名字。</summary>
    private static ModelDefinition[] Models(params string[] names) =>
        [.. names.Select(name => new ModelDefinition { Name = new ModelRef(name) })];

    /// <summary>一条名为默认的流程。</summary>
    private static FlowDefinition Flow(NodeSpec root, params ModelDefinition[] models) =>
        new(new FlowName("默认"), null, models, root);

    /// <summary>流程内某个节点的错误说明。</summary>
    private static string NodeError(string node, string message) =>
        $"流程 默认 的节点 {node}：{message}";

    /// <summary>流程内某个模型选择的错误说明。</summary>
    private static string ModelError(string model, string message) =>
        $"流程 默认 的模型选择 {model}：{message}";

    /// <summary>校验流程，取错误说明。</summary>
    private static IReadOnlyList<string> Errors(FlowDefinition flow) =>
        [.. FlowRules.Validate(flow).ErrorsOrEmptyList.Select(error => error.Description)];
}
