using ErrorOr;
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
    [InlineData(ScopeViolation, "不在可见作用域")]
    [InlineData(FanOutBeforePlan, "按条目展开的叶子必须排在规划叶子之后")]
    [InlineData(NonContiguousExpansion, "连续的展开段")]
    [InlineData(CheckWithoutImplementReference, "检查节点必须用 From 引用被检查的实施产出")]
    [InlineData(UnreferencedImplement, "按条目展开的实施必须被某个检查节点引用")]
    [InlineData(RejectOnImplement, "OnReject 与 MaxAttempts 只适用于检查节点")]
    [InlineData(DuplicateNodeName, "节点名重复")]
    [InlineData(DuplicateAgentName, "agent 名重复")]
    [InlineData(UnknownAgent, "引用的 agent XXX 不存在")]
    [InlineData(FunnelGateBlocked, "收拢检查不支持待批准门控")]
    [InlineData(AttemptsOverCap, "MaxAttempts 超过 Agent:MaxAttempts")]
    [InlineData(NoNodes, "至少要有一个节点")]
    [InlineData(ContainerFrom, "不支持 From")]
    [InlineData(ContainerPrompt, "不支持 Prompt")]
    [InlineData(LeafAfterExpansion, "只能有一个收拢检查叶子收尾")]
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

        string text = File.ReadAllText(Path.Combine(harness.Root, "flows.json"));
        Assert.Contains("\"Output\": \"Plan\"", text);
        Assert.Contains("制定计划", text);
        Assert.Contains("整体检查", text);
    }

    private const string MissingNodeName = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }], "Nodes": [
          { "Agent": "规划者", "Output": "Plan" }
        ] } ] }
        """;

    private const string MissingCheck = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }, { "Name": "执行者" }], "Nodes": [
          { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
          { "Name": "实施", "Agent": "执行者", "From": ["制定计划"] }
        ] } ] }
        """;

    private const string ScopeViolation = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }, { "Name": "执行者" }], "Nodes": [
          { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
          { "Name": "容器", "Nodes": [
            { "Name": "内部", "Agent": "执行者", "From": ["外层收尾"] }
          ] },
          { "Name": "外层收尾", "Agent": "执行者" }
        ] } ] }
        """;

    private const string FanOutBeforePlan = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }, { "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "实施", "Agent": "执行者", "Mode": "PerItem" },
          { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
          { "Name": "检查", "Agent": "检查者", "Output": "Review", "Mode": "PerItem", "From": ["实施"] }
        ] } ] }
        """;

    private const string NonContiguousExpansion = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }, { "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
          { "Name": "实施甲", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
          { "Name": "中间", "Agent": "执行者", "From": ["制定计划"] },
          { "Name": "实施乙", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
          { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["实施甲", "实施乙"] }
        ] } ] }
        """;

    private const string CheckWithoutImplementReference = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
          { "Name": "检查", "Agent": "检查者", "Output": "Review", "From": ["制定计划"] }
        ] } ] }
        """;

    private const string UnreferencedImplement = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }, { "Name": "执行者" }], "Nodes": [
          { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
          { "Name": "实施", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] }
        ] } ] }
        """;
    private const string RejectOnImplement = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }, { "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
          { "Name": "实施", "Agent": "执行者", "From": ["制定计划"], "OnReject": "Stop" },
          { "Name": "检查", "Agent": "检查者", "Output": "Review", "From": ["实施"] }
        ] } ] }
        """;

    private const string DuplicateNodeName = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
          { "Name": "制定计划", "Agent": "规划者" }
        ] } ] }
        """;

    private const string DuplicateAgentName = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }, { "Name": "规划者" }], "Nodes": [
          { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" }
        ] } ] }
        """;

    private const string UnknownAgent = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "制定计划", "Agent": "XXX", "Output": "Plan" }
        ] } ] }
        """;

    private const string FunnelGateBlocked = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }, { "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
          { "Name": "实施", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
          { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["制定计划", "实施"], "Gate": "Review" }
        ] } ] }
        """;

    private const string AttemptsOverCap = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }, { "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
          { "Name": "实施", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
          { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["制定计划", "实施"], "MaxAttempts": 9 }
        ] } ] }
        """;

    private const string NoNodes = """
        { "Flows": [ { "Name": "默认", "Agents": [] } ] }
        """;
    private const string ContainerFrom = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "容器", "From": ["规划"], "Nodes": [ { "Name": "规划", "Agent": "规划者", "Output": "Plan" } ] }
        ] } ] }
        """;

    private const string ContainerPrompt = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "容器", "Prompt": "不落地的提示", "Nodes": [ { "Name": "规划", "Agent": "规划者", "Output": "Plan" } ] }
        ] } ] }
        """;

    private const string LeafAfterExpansion = """
        { "Flows": [ { "Name": "默认", "Agents": [{ "Name": "规划者" }, { "Name": "执行者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
          { "Name": "实施", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
          { "Name": "收尾", "Agent": "执行者", "From": ["制定计划"] },
          { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["制定计划", "实施"], "OnReject": "Retry" }
        ] } ] }
        """;

    private const string SingleFunnelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Agents": [
                { "Name": "规划者" },
                { "Name": "执行者", "Tools": ["GetLocalTime", "GetWeather"] },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan", "Prompt": "拆分条目。" },
                { "Name": "分配执行", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Retry", "MaxAttempts": 2 }
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
              "Agents": [
                { "Name": "规划者" },
                { "Name": "执行者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
                { "Name": "分配执行", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Retry" }
              ]
            }
          ]
        }
        """;
}