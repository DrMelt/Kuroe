using ErrorOr;
using Kuroe.Configuration;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>流程配置的校验与导入。</summary>
public sealed class WorkflowValidationTests
{
    [Theory]
    [InlineData(MissingNodeName, "节点名不能为空")]
    [InlineData(MissingCheck, "流程需要有检查节点")]
    [InlineData(ScopeViolation, "From 引用的节点 不存在 不在流程里")]
    [InlineData(FanOutBeforePlan, "按条目展开的执行节点必须从规划执行节点或其它展开执行节点取输入")]
    [InlineData(ExpansionWithoutPlan, "按条目展开的执行节点只能从规划执行节点取拆分")]
    [InlineData(CyclicReference, "流程引用关系存在环")]
    [InlineData(ParallelBranchNotPerItem, "声明分支 撰写 的执行节点必须按条目展开并从规划执行节点取拆分")]
    [InlineData(CheckWithoutImplementReference, "检查节点必须用 From 引用被检查的实施产出")]
    [InlineData(UnreferencedImplement, "按条目展开的实施必须被某个检查节点引用")]
    [InlineData(RejectOnImplement, "OnReject 与 MaxAttempts 只适用于检查节点")]
    [InlineData(DuplicateNodeName, "节点名重复")]
    [InlineData(DuplicateModelName, "模型配置名重复")]
    [InlineData(UnknownModel, "引用的模型配置 XXX 不存在")]
    [InlineData(AttemptsOverCap, "MaxAttempts 超过 Runtime:MaxAttempts")]
    [InlineData(MaxAttemptsZero, "MaxAttempts 必须为正整数")]
    [InlineData(NoNodes, "至少要有一个节点")]
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
    [InlineData(AlignedAcrossSpaces, "逐条对齐的两端必须来自同一个拆分")]
    [InlineData(SelfReferenceContainer, "流程引用关系存在环")]
    [InlineData(PartialSelfReferenceContainer, "流程引用关系存在环")]
    [InlineData(CrossContainerReference, "流程引用关系存在环")]
    public void Invalid_flow_fails_setup(string flowsJson, string expected)
    {
        ErrorOr<KuroeHarness> harness = KuroeHarness.TryCreate(flowsJson);

        Assert.True(harness.IsError);
        Assert.Contains(harness.ErrorsOrEmptyList, error => error.Description.Contains(expected));
    }

    [Fact]
    public void Valid_custom_flow_loads_and_imports()
    {
        using KuroeHarness harness = KuroeHarness.Create(SingleFunnelFlow);
        Assert.Contains(harness.Flows.All(), flow => flow.Name == "默认");

        string source = Path.Combine(Path.GetTempPath(), $"kuroe-flow-{Guid.NewGuid():N}.json");
        File.WriteAllText(source, TwoCustomFlows);
        try
        {
            WorkflowImport imported = harness.Flows.Import(source).ThrowIfError();

            Assert.Single(imported.Names);
            Assert.Contains(harness.Flows.All(), flow => flow.Name == "两级");
            Assert.Equal(3, harness.Flows.Find("两级").ThrowIfError().Nodes.Count);
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public void Import_resolves_a_relative_path_against_the_work_directory()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "extra.json"), SingleFunnelFlow);

        WorkflowImport imported = harness.Flows.Import("extra.json").ThrowIfError();

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

        string text = File.ReadAllText(KuroePaths.At(harness.Root).WorkflowFile);
        Assert.Contains("\"Output\": \"Plan\"", text);
        Assert.Contains("制定计划", text);
        Assert.Contains("整体检查", text);
    }

    private const string MissingNodeName = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Nodes": [
          { "Model": "规划者", "Output": "Plan" }
        ] } ] }
        """;

    private const string BadNodeMode = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Mode": "PerItemm" }
        ] } ] }
        """;

    private const string MissingCheck = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "执行者" }], "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "实施", "Model": "执行者", "From": ["制定计划"] }
        ] } ] }
        """;

    private const string ScopeViolation = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "执行者" }], "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "容器", "Nodes": [
            { "Name": "内部", "Model": "执行者", "From": ["不存在"] }
          ] }
        ] } ] }
        """;

    private const string FanOutBeforePlan = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "实施", "Model": "执行者", "Mode": "PerItem" },
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "检查", "Model": "检查者", "Output": "Review", "Mode": "PerItem", "From": ["实施"] }
        ] } ] }
        """;

    private const string CheckWithoutImplementReference = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "检查", "Model": "检查者", "Output": "Review", "From": ["制定计划"] }
        ] } ] }
        """;

    private const string UnreferencedImplement = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "执行者" }], "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "实施", "Model": "执行者", "Mode": "PerItem", "From": ["制定计划"] }
        ] } ] }
        """;
    private const string RejectOnImplement = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "实施", "Model": "执行者", "From": ["制定计划"], "OnReject": "Stop" },
          { "Name": "检查", "Model": "检查者", "Output": "Review", "From": ["实施"] }
        ] } ] }
        """;

    private const string DuplicateNodeName = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "制定计划", "Model": "规划者" }
        ] } ] }
        """;

    private const string DuplicateModelName = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "规划者" }], "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" }
        ] } ] }
        """;

    private const string UnknownModel = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "制定计划", "Model": "XXX", "Output": "Plan" }
        ] } ] }
        """;

    private const string AttemptsOverCap = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "实施", "Model": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
          { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "实施"], "MaxAttempts": 9 }
        ] } ] }
        """;

    private const string MaxAttemptsZero = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "实施", "Model": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
          { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "实施"], "MaxAttempts": 0 }
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
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "撰写", "Model": "执行者", "Branch": "撰写", "From": ["制定计划"] },
          { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "撰写"], "OnReject": "Retry" }
        ] } ] }
        """;

    private const string ExpansionWithoutPlan = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "准备", "Model": "执行者" },
          { "Name": "实施", "Model": "执行者", "Mode": "PerItem", "From": ["准备"] },
          { "Name": "检查", "Model": "检查者", "Output": "Review", "Mode": "PerItem", "From": ["实施"] }
        ] } ] }
        """;

    private const string CyclicReference = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "甲", "Model": "执行者", "From": ["乙"] },
          { "Name": "乙", "Model": "执行者", "From": ["甲"] },
          { "Name": "检查", "Model": "检查者", "Output": "Review", "From": ["甲"] }
        ] } ] }
        """;

    private const string SingleFunnelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者" },
                { "Name": "执行者", "Tools": ["GetLocalTime", "GetWeather"] },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Prompt": "拆分条目。" },
                { "Name": "分配执行", "Model": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Retry", "MaxAttempts": 2 }
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
                { "Name": "执行者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "分配执行", "Model": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Retry" }
              ]
            }
          ]
        }
        """;

    private const string SplitOnImplement = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "实施", "Model": "执行者",
            "Split": { "Items": [ { "Title": "甲", "Instruction": "做甲" } ] } }
        ] } ] }
        """;

    private const string SplitEmpty = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "Split": {} }
        ] } ] }
        """;

    private const string SplitExtrasOverCap = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "Split": { "ExtrasMax": 21 } }
        ] } ] }
        """;

    private const string SplitZeroWithoutItems = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "Split": { "ExtrasMax": 0 } }
        ] } ] }
        """;

    private const string SplitStaticPrompt = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "Prompt": "不需要",
            "Split": { "Items": [ { "Title": "甲", "Instruction": "做甲" } ] } }
        ] } ] }
        """;

    private const string SplitStaticGate = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "Gate": "Review",
            "Split": { "Items": [ { "Title": "甲", "Instruction": "做甲" } ] } }
        ] } ] }
        """;

    private const string SplitStaticFrom = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "上游", "Model": "执行者" },
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "From": ["上游"],
            "Split": { "Items": [ { "Title": "甲", "Instruction": "做甲" } ] } }
        ] } ] }
        """;

    private const string SplitParallelBranch = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定计划", "Model": "执行者", "Output": "Plan",
            "Split": { "Items": [ { "Title": "甲", "Instruction": "做甲", "Branch": "不存在" } ] } },
          { "Name": "撰写", "Model": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
          { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "撰写"], "OnReject": "Retry" }
        ] } ] }
        """;

    private const string ContainerModeRejected = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "实施", "Mode": "Parallel",
            "Nodes": [ { "Name": "撰写", "Model": "执行者" } ] }
        ] } ] }
        """;

    private const string AlignedAcrossSpaces = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定A计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "制定B计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "实施A", "Model": "执行者", "Mode": "PerItem", "From": ["制定A计划"] },
          { "Name": "实施B", "Model": "执行者", "Mode": "PerItem", "From": ["制定B计划"] },
          { "Name": "逐条检查", "Model": "检查者", "Output": "Review", "Mode": "PerItem", "From": ["实施A", "实施B"] }
        ] } ] }
        """;

    private const string SelfReferenceContainer = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "交付", "Nodes": [
            { "Name": "撰写", "Model": "执行者", "From": ["交付"] }
          ] },
          { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["交付"], "OnReject": "Retry" }
        ] } ] }
        """;

    private const string PartialSelfReferenceContainer = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "交付", "Nodes": [
            { "Name": "撰写", "Model": "执行者", "From": ["交付"] },
            { "Name": "排版", "Model": "执行者" }
          ] },
          { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["交付"], "OnReject": "Retry" }
        ] } ] }
        """;

    private const string CrossContainerReference = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "容器甲", "Nodes": [
            { "Name": "甲", "Model": "执行者", "From": ["容器乙"] }
          ] },
          { "Name": "容器乙", "Nodes": [
            { "Name": "乙", "Model": "执行者", "From": ["容器甲"] }
          ] },
          { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["容器甲"], "OnReject": "Retry" }
        ] } ] }
        """;
}