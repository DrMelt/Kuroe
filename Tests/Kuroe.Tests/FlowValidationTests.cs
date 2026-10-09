using ErrorOr;
using Kuroe.Configuration;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>流程配置的校验与导入。</summary>
public sealed class FlowValidationTests
{
    [Theory]
    [InlineData(MissingNodeName, "节点名不能为空")]
    [InlineData(ScopeViolation, "From 引用的节点 不存在 不在流程里")]
    [InlineData(FanOutBeforePlan, "按条目展开的执行节点必须从规划执行节点或其它展开执行节点取输入")]
    [InlineData(ExpansionWithoutPlan, "按条目展开的执行节点只能从规划执行节点取拆分")]
    [InlineData(CyclicReference, "没有环外来源")]
    [InlineData(ParallelBranchNotPerItem, "声明分支 撰写 的执行节点必须按条目展开并从规划执行节点取拆分")]
    [InlineData(DuplicateNodeName, "节点名重复")]
    [InlineData(DuplicateModelName, "模型选择名重复")]
    [InlineData(UnknownModel, "引用的模型选择 XXX 不存在")]
    [InlineData(NoNodes, "流程缺少根节点")]
    [InlineData(MultipleRootNodes, "流程只能有一个根节点")]
    [InlineData(ContainerFrom, "不支持 From")]
    [InlineData(ContainerPrompt, "不支持 Prompt")]
    [InlineData(BadNodeMode, "Mode 应为 Single 或 PerItem")]
    [InlineData(SplitOnImplement, "Split 只能写在规划节点上")]
    [InlineData(SplitEmpty, "Split 至少要声明 Items 或 ExtrasMax")]
    [InlineData(SplitExtrasOverCap, "Split.ExtrasMax 必须是 0 到 20 的整数")]
    [InlineData(SplitZeroWithoutItems, "Split.ExtrasMax 为 0 时要求声明至少一条 Items")]
    [InlineData(SplitStaticPrompt, "纯静态拆分节点不支持 Prompt")]
    [InlineData(SplitStaticGate, "纯静态拆分节点不支持待批准门控")]
    [InlineData(SplitStaticFrom, "纯静态拆分节点不支持 From")]
    [InlineData(SplitParallelBranch, "没有对应的分支执行节点")]
    [InlineData(ContainerModeRejected, "容器不支持 Mode")]
    [InlineData(MaxRunsZero, "MaxRuns 必须是正整数")]
    [InlineData(MaxRunsNegative, "MaxRuns 必须是正整数")]
    [InlineData(ContainerMaxRunsZero, "MaxRuns 必须是正整数")]
    [InlineData(ContainerMaxRunsNegative, "MaxRuns 必须是正整数")]
    [InlineData(LibraryExecutionMaxRunsZero, "MaxRuns 必须是正整数")]
    [InlineData(LibraryContainerMaxRunsZero, "MaxRuns 必须是正整数")]
    [InlineData(AlignedAcrossSpaces, "逐条对齐的两端必须来自同一个拆分")]
    [InlineData(SelfReferenceContainer, "不能从自身所在容器")]
    [InlineData(PartialSelfReferenceContainer, "不能从自身所在容器")]
    [InlineData(CrossContainerReference, "没有环外来源")]
    [InlineData(BadToolPathSegment, "不能为空或含空白与花括号")]
    [InlineData(BadToolPathSlash, "不能以 / 开头或结尾")]
    [InlineData(InputWithTools, "不能声明 Tools")]
    [InlineData(InputWithPrompt, "不能声明 Prompt")]
    [InlineData(InputWithModel, "不能声明模型选择")]
    [InlineData(InputPerItem, "只能整节点等待用户输入")]
    [InlineData(InputWithBranch, "不能声明 Branch")]
    [InlineData(InputWithAnyOf, "不能声明 AnyOf")]
    [InlineData(InputWithValidate, "不能声明 Validate")]
    [InlineData(InputGateReview, "不能声明 Review 门控")]
    [InlineData(InputWithIn, "输入节点不启动 run，不能声明输入端口绑定。")]
    [InlineData(InputWithSplit, "Split 只能写在规划节点上")]
    [InlineData(QuestionOnImplement, "Question 只属于输入节点")]
    [InlineData(PortRefMissing, "没有输出端口")]
    [InlineData(PortOnPlan, "输出端口只能声明在整节点文本产出上")]
    [InlineData(PortOnInput, "不能声明输出端口")]
    [InlineData(InputWithSystemPrompt, "不能声明系统指令")]
    [InlineData(OutputsReservedContext, "是保留名")]
    [InlineData(OutputsReservedContextInput, "是保留名")]
    [InlineData(OutputPortWithAt, "端口名不能为空或含 @")]
    [InlineData(OutputPortBlank, "端口名不能为空或含 @")]
    [InlineData(ContainerOutPortMissing, "容器 交付 没有输出端口")]
    [InlineData(ContainerOutCrossContainer, "不能从容器外直接引用容器内成员")]
    [InlineData(FromInputContext, "不能作为上下文端口来源")]
    [InlineData(ContextInputWithExtraKey, "只能声明隐式 ContextInput 端口")]
    [InlineData(ContextInputFromInput, "ContextInput 端口绑定来源")]
    [InlineData(ContextInputFromPerItem, "不能是按条目展开")]
    [InlineData(PortDuplicate, "输出端口 结论 重复")]
    [InlineData(ReferenceWithOutputs, "引用节点不能声明输出端口与系统指令")]
    [InlineData(NodeNameWithAt, "节点名不能含 @")]
    public void Invalid_flow_fails_setup(string flowsJson, string expected)
    {
        ErrorOr<KuroeHarness> harness = KuroeHarness.TryCreate(flowsJson);

        Assert.True(harness.IsError);
        Assert.Contains(harness.ErrorsOrEmptyList, error => error.Description.Contains(expected));
    }

    /// <summary>引用环内含输入节点时没有环外来源也可启动，输入节点是挂点也是周期外源。</summary>
    [Fact]
    public void Loop_with_input_node_loads_while_plain_loop_is_rejected()
    {
        using KuroeHarness harness = KuroeHarness.Create(LoopWithInputNode);

        Assert.Contains(harness.Flows.All(), flow => flow.Name == new FlowName("默认"));
    }

    [Fact]
    public void Valid_custom_flow_loads_and_imports()
    {
        using KuroeHarness harness = KuroeHarness.Create(SingleFunnelFlow);
        Assert.Contains(harness.Flows.All(), flow => flow.Name == new FlowName("默认"));

        string source = Path.Combine(Path.GetTempPath(), $"kuroe-flow-{Guid.NewGuid():N}.json");
        File.WriteAllText(source, TwoCustomFlows);
        try
        {
            FlowImport imported = harness.Flows.Import(source).ThrowIfError();

            Assert.Single(imported.Names);
            Assert.Contains(harness.Flows.All(), flow => flow.Name == new FlowName("两级"));
            FlowDefinition importedFlow = harness.Flows.Find(new FlowName("两级")).ThrowIfError();
            Assert.Equal(2, importedFlow.RootNode.Nodes!.Count);
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public void Flow_name_with_surrounding_spaces_is_normalized()
    {
        using KuroeHarness harness = KuroeHarness.Create(FlowNameWithSurroundingSpaces);

        FlowDefinition flow = Assert.Single(harness.Flows.All());
        Assert.Equal(new FlowName("默认"), flow.Name);
        Assert.True(harness.Flows.Find(new FlowName("默认")).IsSuccess);
    }

    [Fact]
    public void Import_resolves_a_relative_path_against_the_work_directory()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "extra.json"), SingleFunnelFlow);

        FlowImport imported = harness.Flows.Import("extra.json").ThrowIfError();

        Assert.Single(imported.Names);
        Assert.True(Path.IsPathRooted(imported.Source), $"导入来源应是绝对路径，收到 {imported.Source}。");
    }

    /// <summary>写回的流程文件保留缩进与可读中文，枚举写成名字。</summary>
    [Fact]
    public void Saved_flow_file_keeps_readable_text_and_string_enums()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "extra.json"), SingleFunnelFlow);

        harness.Flows.Import("extra.json").ThrowIfError();

        string text = File.ReadAllText(KuroePaths.At(harness.Root).FlowsFile);
        Assert.Contains("\"Output\": \"Plan\"", text);
        Assert.Contains("制定计划", text);
        Assert.Contains("分配执行", text);
    }

    private const string MissingNodeName = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Model": "规划者", "Output": "Plan" }
          ] }
        ] } ] }
        """;

    private const string BadNodeMode = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Mode": "PerItemm" }
          ] }
        ] } ] }
        """;

    private const string ScopeViolation = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "容器", "Nodes": [
            { "Name": "内部", "Model": "执行者", "From": ["不存在"] }
          ] }
          ] }
        ] } ] }
        """;

    private const string FanOutBeforePlan = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "实施", "Model": "执行者", "Mode": "PerItem" },
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "收尾", "Model": "执行者", "Mode": "PerItem", "From": ["实施"] }
          ] }
        ] } ] }
        """;

    private const string DuplicateNodeName = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "制定计划", "Model": "规划者" }
          ] }
        ] } ] }
        """;

    private const string DuplicateModelName = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "规划者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" }
          ] }
        ] } ] }
        """;

    private const string UnknownModel = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定计划", "Model": "XXX", "Output": "Plan" }
          ] }
        ] } ] }
        """;

    private const string NoNodes = """
        { "Flows": [ { "Name": "默认", "Models": [] } ] }
        """;
    private const string ContainerFrom = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "容器", "From": ["规划"], "Nodes": [ { "Name": "规划", "Model": "规划者", "Output": "Plan" } ] }
        ] } ] }
        """;

    private const string ContainerPrompt = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "容器", "Prompt": "不落地的提示", "Nodes": [ { "Name": "规划", "Model": "规划者", "Output": "Plan" } ] }
        ] } ] }
        """;

    private const string ParallelBranchNotPerItem = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "撰写", "Model": "执行者", "Branch": "撰写", "From": ["制定计划"] }
          ] }
        ] } ] }
        """;

    private const string ExpansionWithoutPlan = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "准备", "Model": "执行者" },
          { "Name": "实施", "Model": "执行者", "Mode": "PerItem", "From": ["准备"] },
          { "Name": "收尾", "Model": "执行者", "Mode": "PerItem", "From": ["实施"] }
          ] }
        ] } ] }
        """;

    private const string CyclicReference = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "甲", "Model": "执行者", "From": ["乙"] },
          { "Name": "乙", "Model": "执行者", "From": ["甲"] }
          ] }
        ] } ] }
        """;

    /// <summary>引用环内含输入节点，输入节点是挂点也是周期外源，无需环外来源即可启动。</summary>
    private const string LoopWithInputNode = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收话", "Output": "Input", "From": ["回话"] },
            { "Name": "回话", "Output": "Text", "Model": "执行者", "From": ["收话"] }
          ] }
        ] } ] }
        """;

    private const string SingleFunnelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者" },
                { "Name": "执行者", "Tools": ["GetLocalTime"] }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Prompt": "拆分条目。" },
                    { "Name": "分配执行", "Model": "执行者", "Mode": "PerItem", "From": ["制定计划"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string TwoCustomFlows = """
        {
          "Flows": [
            {
              "Name": "两级",
              "Models": [
                { "Name": "规划者" },
                { "Name": "执行者" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                    { "Name": "分配执行", "Model": "执行者", "Mode": "PerItem", "From": ["制定计划"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string SplitOnImplement = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "实施", "Model": "执行者",
            "Split": { "Items": [ { "Title": "甲", "Instruction": "做甲" } ] } }
          ] }
        ] } ] }
        """;

    private const string SplitEmpty = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "Split": {} }
          ] }
        ] } ] }
        """;

    private const string SplitExtrasOverCap = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "Split": { "ExtrasMax": 21 } }
          ] }
        ] } ] }
        """;

    private const string SplitZeroWithoutItems = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "Split": { "ExtrasMax": 0 } }
          ] }
        ] } ] }
        """;

    private const string SplitStaticPrompt = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "Prompt": "不需要",
            "Split": { "Items": [ { "Title": "甲", "Instruction": "做甲" } ] } }
          ] }
        ] } ] }
        """;

    private const string SplitStaticGate = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "Gate": "Review",
            "Split": { "Items": [ { "Title": "甲", "Instruction": "做甲" } ] } }
          ] }
        ] } ] }
        """;

    private const string SplitStaticFrom = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "上游", "Model": "执行者" },
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "From": ["上游"],
            "Split": { "Items": [ { "Title": "甲", "Instruction": "做甲" } ] } }
          ] }
        ] } ] }
        """;

    private const string SplitParallelBranch = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan",
            "Split": { "Items": [ { "Title": "甲", "Instruction": "做甲", "Branch": "不存在" } ] } },
          { "Name": "撰写", "Model": "执行者", "Mode": "PerItem", "From": ["制定计划"] }
          ] }
        ] } ] }
        """;

    private const string ContainerModeRejected = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "实施", "Mode": "Parallel",
            "Nodes": [ { "Name": "撰写", "Model": "执行者" } ] }
        ] } ] }
        """;

    private const string AlignedAcrossSpaces = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定A计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "制定B计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "实施A", "Model": "执行者", "Mode": "PerItem", "From": ["制定A计划"] },
          { "Name": "实施B", "Model": "执行者", "Mode": "PerItem", "From": ["制定B计划"] },
          { "Name": "收尾", "Model": "执行者", "Mode": "PerItem", "From": ["实施A", "实施B"] }
          ] }
        ] } ] }
        """;

    private const string SelfReferenceContainer = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "交付", "Nodes": [
            { "Name": "撰写", "Model": "执行者", "From": ["交付"] }
          ] }
          ] }
        ] } ] }
        """;

    private const string PartialSelfReferenceContainer = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "交付", "Nodes": [
            { "Name": "撰写", "Model": "执行者", "From": ["交付"] },
            { "Name": "排版", "Model": "执行者" }
          ] }
          ] }
        ] } ] }
        """;

    private const string CrossContainerReference = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "容器甲", "Nodes": [
            { "Name": "甲", "Model": "执行者", "From": ["容器乙"] }
          ] },
          { "Name": "容器乙", "Nodes": [
            { "Name": "乙", "Model": "执行者", "From": ["容器甲"] }
          ] }
          ] }
        ] } ] }
        """;

    /// <summary>能力工具白名单声明含空白的非法路径，装载期拒绝。</summary>
    private const string BadToolPathSegment = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "干活", "Model": "执行者", "Tools": ["a b"] }
          ] }
        ] } ] }
        """;

    /// <summary>能力工具白名单声明以 / 结尾的非法路径，装载期拒绝。</summary>
    private const string BadToolPathSlash = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "干活", "Model": "执行者", "Tools": ["git/"] }
          ] }
        ] } ] }
        """;

    private const string MultipleRootNodes = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "实施", "Model": "执行者" },
          { "Name": "收尾", "Model": "执行者", "From": ["实施"] }
        ] } ] }
        """;

    private const string MaxRunsZero = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "干活", "Model": "执行者", "MaxRuns": 0 }
          ] }
        ] } ] }
        """;

    private const string InputWithTools = """
        { "Flows": [ { "Name": "默认", "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收集", "Output": "Input", "Tools": ["GetLocalTime"] }
          ] }
        ] } ] }
        """;

    private const string InputWithPrompt = """
        { "Flows": [ { "Name": "默认", "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收集", "Output": "Input", "Prompt": "多想想" }
          ] }
        ] } ] }
        """;

    private const string InputWithModel = """
        { "Flows": [ { "Name": "默认", "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收集", "Output": "Input", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    private const string InputPerItem = """
        { "Flows": [ { "Name": "默认", "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收集", "Output": "Input", "Mode": "PerItem" }
          ] }
        ] } ] }
        """;

    private const string InputWithBranch = """
        { "Flows": [ { "Name": "默认", "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收集", "Output": "Input", "Branch": "甲" }
          ] }
        ] } ] }
        """;

    private const string InputWithAnyOf = """
        { "Flows": [ { "Name": "默认", "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收集", "Output": "Input", "AnyOf": [["来源"]] }
          ] }
        ] } ] }
        """;

    private const string InputWithValidate = """
        { "Flows": [ { "Name": "默认", "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收集", "Output": "Input", "Validate": { "Predicate": "NonEmpty" } }
          ] }
        ] } ] }
        """;

    private const string InputGateReview = """
        { "Flows": [ { "Name": "默认", "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收集", "Output": "Input", "Gate": "Review" }
          ] }
        ] } ] }
        """;

    private const string InputWithSplit = """
        { "Flows": [ { "Name": "默认", "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收集", "Output": "Input", "Split": { "Items": [{ "Title": "甲" }] } }
          ] }
        ] } ] }
        """;

    /// <summary>输入节点声明输入端口绑定，装配校验拒绝。</summary>
    private const string InputWithIn = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收集", "Output": "Input", "In": { "ContextInput": "准备" } },
            { "Name": "准备", "Output": "Text", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    private const string MaxRunsNegative = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "干活", "Model": "执行者", "MaxRuns": -3 }
          ] }
        ] } ] }
        """;

    private const string ContainerMaxRunsZero = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "MaxRuns": 0, "Nodes": [
            { "Name": "干活", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    private const string ContainerMaxRunsNegative = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "MaxRuns": -2, "Nodes": [
            { "Name": "干活", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    /// <summary>库执行定义声明非正上限，未被引用也在库校验阶段拒绝。</summary>
    private const string LibraryExecutionMaxRunsZero = """
        { "Nodes": [ { "Name": "干活库", "Tools": [], "MaxRuns": 0 } ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "干活", "Model": "执行者" }
            ] }
          ] } ] }
        """;

    /// <summary>库容器定义声明非正统一上限，未被引用也在库校验阶段拒绝。</summary>
    private const string LibraryContainerMaxRunsZero = """
        { "Nodes": [ { "Name": "干活组", "MaxRuns": 0, "Nodes": [
            { "Name": "干活", "Model": "执行者" }
          ] } ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "干活", "Model": "执行者" }
            ] }
          ] } ] }
        """;
    /// <summary>From 引用不存在的输出端口，装配校验拒绝。</summary>
    private const string PortRefMissing = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "分析", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] },
            { "Name": "实施", "Output": "Text", "Model": "执行者", "From": ["分析@不存在"] }
          ] }
        ] } ] }
        """;

    /// <summary>输出端口声明在规划节点上，装配校验拒绝。</summary>
    private const string PortOnPlan = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "分析", "Output": "Plan", "Model": "执行者", "Outputs": ["结论"] }
          ] }
        ] } ] }
        """;

    /// <summary>输入节点声明输出端口，装配校验拒绝。</summary>
    private const string PortOnInput = """
        { "Flows": [ { "Name": "默认", "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收集", "Output": "Input", "Outputs": ["结论"] }
          ] }
        ] } ] }
        """;

    /// <summary>非输入执行节点声明 Question，装配校验拒绝。</summary>
    private const string QuestionOnImplement = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "干活", "Output": "Text", "Model": "执行者", "Question": "你想问什么" }
          ] }
        ] } ] }
        """;

    /// <summary>输入节点声明系统指令，装配校验拒绝。</summary>
    private const string InputWithSystemPrompt = """
        { "Flows": [ { "Name": "默认", "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收集", "Output": "Input", "SystemPrompt": ["背景"] }
          ] }
        ] } ] }
        """;

    /// <summary>输出端口声明保留名，装配校验拒绝。</summary>
    private const string OutputsReservedContext = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "分析", "Output": "Text", "Model": "执行者", "Outputs": ["ContextOutput"] }
          ] }
        ] } ] }
        """;

    /// <summary>输出端口声明输入端口保留名，装配校验拒绝。</summary>
    private const string OutputsReservedContextInput = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "分析", "Output": "Text", "Model": "执行者", "Outputs": ["ContextInput"] }
          ] }
        ] } ] }
        """;

    /// <summary>输入节点作为上下文端口来源，装配校验拒绝。</summary>
    private const string FromInputContext = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "用户输入", "Output": "Input" },
            { "Name": "实施", "Output": "Text", "Model": "执行者", "From": ["用户输入@ContextOutput"] }
          ] }
        ] } ] }
        """;

    /// <summary>上下文输入端口绑定之外的键，装配校验拒绝。</summary>
    private const string ContextInputWithExtraKey = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "准备", "Output": "Text", "Model": "执行者" },
            { "Name": "实施", "Output": "Text", "Model": "执行者", "In": { "ContextInput": "准备", "其它": "准备" } }
          ] }
        ] } ] }
        """;

    /// <summary>上下文输入端口绑定输入节点，装配校验拒绝。</summary>
    private const string ContextInputFromInput = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "用户输入", "Output": "Input" },
            { "Name": "实施", "Output": "Text", "Model": "执行者", "In": { "ContextInput": "用户输入" } }
          ] }
        ] } ] }
        """;

    /// <summary>上下文输入端口绑定按条目展开的节点，装配校验拒绝。</summary>
    private const string ContextInputFromPerItem = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "制定", "Output": "Plan", "Model": "执行者" },
            { "Name": "实施", "Output": "Text", "Model": "执行者", "Mode": "PerItem", "From": ["制定"] },
            { "Name": "汇报", "Output": "Text", "Model": "执行者", "In": { "ContextInput": "实施" } }
          ] }
        ] } ] }
        """;

    /// <summary>输出端口名重复，装配校验拒绝。</summary>
    private const string PortDuplicate = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "分析", "Output": "Text", "Model": "执行者", "Outputs": ["结论", "结论"] }
          ] }
        ] } ] }
        """;

    /// <summary>引用执行节点声明输出端口，装配校验拒绝。</summary>
    private const string ReferenceWithOutputs = """
        { "Nodes": [ { "Name": "分析库", "Output": "Text" } ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "分析", "Use": "分析库", "Model": "执行者", "Outputs": ["结论"] }
            ] }
          ] } ]
        }
        """;

    /// <summary>输出端口名含 @，与端口引用分隔符冲突，装配校验拒绝。</summary>
    private const string OutputPortWithAt = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "分析", "Output": "Text", "Model": "执行者", "Outputs": ["结论@甲"] }
          ] }
        ] } ] }
        """;

    /// <summary>输出端口名空白，装配校验拒绝。</summary>
    private const string OutputPortBlank = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "分析", "Output": "Text", "Model": "执行者", "Outputs": ["  "] }
          ] }
        ] } ] }
        """;

    /// <summary>流程名带首尾空白，装配时按解析口规范化。</summary>
    private const string FlowNameWithSurroundingSpaces = """
        { "Flows": [ { "Name": "  默认 ", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "分析", "Output": "Text", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    /// <summary>流程内节点名含 @，与端口引用分隔符冲突，装配校验拒绝。</summary>
    private const string NodeNameWithAt = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "甲@乙", "Output": "Text", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    /// <summary>引用容器上不存在的输出端口，装配校验拒绝。</summary>
    private const string ContainerOutPortMissing = """
        { "Nodes": [
            { "Name": "允许工具", "Output": "Text" },
            {
              "Name": "交付",
              "Out": { "结论": "实施" },
              "Nodes": [ { "Name": "实施", "Use": "允许工具", "Model": "执行者" } ]
            }
          ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } },
              { "Name": "复盘", "Output": "Text", "Model": "执行者", "From": ["交付@不存在"] }
            ] }
          ] } ]
        }
        """;

    /// <summary>装配层直接引用容器实例内部成员，容器封装拒绝，对外只暴露输出端口。</summary>
    private const string ContainerOutCrossContainer = """
        { "Nodes": [
            { "Name": "允许工具", "Output": "Text" },
            {
              "Name": "交付",
              "Out": { "结论": "实施" },
              "Nodes": [ { "Name": "实施", "Use": "允许工具", "Model": "执行者" } ]
            }
          ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } },
              { "Name": "复盘", "Output": "Text", "Model": "执行者", "From": ["交付.实施"] }
            ] }
          ] } ]
        }
        """;
}
