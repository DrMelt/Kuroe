using ErrorOr;
using Kuroe.Workflows.Flows;
using Xunit;

namespace Kuroe.Tests;

/// <summary>流程配置的校验与导入。</summary>
public sealed class WorkflowValidationTests
{
    [Theory]
    [InlineData(MissingStepName, "步骤名不能为空")]
    [InlineData(MissingCheck, "实施之后要有检查步骤")]
    [InlineData(ForwardFrom, "From 只能引用更早的步骤")]
    [InlineData(FanOutBeforePlan, "按条目展开的步骤必须排在规划步骤之后")]
    [InlineData(SingleAfterFanOut, "展开条目之后的步骤必须同样按条目展开")]
    [InlineData(CheckWithoutImplementReference, "检查步骤必须用 From 引用被检查的实施产出")]
    [InlineData(RejectOnImplement, "OnReject 与 MaxAttempts 只适用于检查步骤")]
    [InlineData(TwoPlans, "一条流程只能有一个规划步骤")]
    [InlineData(AttemptsOverCap, "MaxAttempts 超过 Agent:MaxAttempts")]
    [InlineData(NoSteps, "至少要有个步骤")]
    public void Invalid_flow_fails_setup(string flowsJson, string expected)
    {
        ErrorOr<KuroeHarness> harness = KuroeHarness.TryCreate(flowsJson);

        Assert.True(harness.IsError);
        Assert.Contains(harness.ErrorsOrEmptyList, error => error.Description.Contains(expected));
    }

    [Fact]
    public void Valid_custom_flow_loads_and_imports()
    {
        using KuroeHarness harness = KuroeHarness.Create(SingleStepPlan);
        Assert.Contains(harness.Flows.All(), flow => flow.Name == "默认");

        string source = Path.Combine(Path.GetTempPath(), $"kuroe-flow-{Guid.NewGuid():N}.json");
        File.WriteAllText(source, TwoStepPlan);
        try
        {
            WorkflowImport imported = harness.Flows.Import(source).ThrowIfError();

            Assert.Equal(2, imported.Names.Count);
            Assert.Contains(harness.Flows.All(), flow => flow.Name == "只规划");
            Assert.Single(harness.Flows.Find("只规划").ThrowIfError().Steps);
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
        File.WriteAllText(Path.Combine(harness.Root, "extra.json"), TwoStepPlan);

        WorkflowImport imported = harness.Flows.Import("extra.json").ThrowIfError();

        Assert.Equal(2, imported.Names.Count);
        Assert.True(Path.IsPathRooted(imported.Source), $"导入来源应是绝对路径，收到 {imported.Source}。");
    }

    /// <summary>写回的流程文件保留缩进与可读中文，枚举写成名字。</summary>
    [Fact]
    public void Saved_flow_file_keeps_readable_text_and_string_enums()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "extra.json"), TwoStepPlan);

        harness.Flows.Import("extra.json").ThrowIfError();

        string text = File.ReadAllText(Path.Combine(harness.Root, "flows.json"));
        Assert.Contains("\"Role\": \"Plan\"", text);
        Assert.Contains("只规划", text);
    }

    private const string MissingStepName = """
        { "Flows": [ { "Name": "默认", "Steps": [ { "Role": "Plan" } ] } ] }
        """;

    private const string MissingCheck = """
        { "Flows": [ { "Name": "默认", "Steps": [
          { "Name": "规划", "Role": "Plan" },
          { "Name": "实施", "Role": "Implement", "Scope": "PerItem", "From": ["规划"] }
        ] } ] }
        """;

    private const string ForwardFrom = """
        { "Flows": [ { "Name": "默认", "Steps": [
          { "Name": "规划", "Role": "Plan", "From": ["实施"] },
          { "Name": "实施", "Role": "Implement" },
          { "Name": "检查", "Role": "Check", "From": ["实施"] }
        ] } ] }
        """;

    private const string FanOutBeforePlan = """
        { "Flows": [ { "Name": "默认", "Steps": [
          { "Name": "实施", "Role": "Implement", "Scope": "PerItem" },
          { "Name": "检查", "Role": "Check", "Scope": "PerItem", "From": ["实施"] },
          { "Name": "规划", "Role": "Plan" }
        ] } ] }
        """;

    private const string SingleAfterFanOut = """
        { "Flows": [ { "Name": "默认", "Steps": [
          { "Name": "规划", "Role": "Plan" },
          { "Name": "实施", "Role": "Implement", "Scope": "PerItem", "From": ["规划"] },
          { "Name": "检查", "Role": "Check", "From": ["实施"] }
        ] } ] }
        """;

    private const string CheckWithoutImplementReference = """
        { "Flows": [ { "Name": "默认", "Steps": [
          { "Name": "规划", "Role": "Plan" },
          { "Name": "实施", "Role": "Implement", "Scope": "PerItem", "From": ["规划"] },
          { "Name": "检查", "Role": "Check", "Scope": "PerItem", "From": ["规划"] }
        ] } ] }
        """;

    private const string RejectOnImplement = """
        { "Flows": [ { "Name": "默认", "Steps": [
          { "Name": "规划", "Role": "Plan" },
          { "Name": "实施", "Role": "Implement", "Scope": "PerItem", "From": ["规划"], "OnReject": "Stop" },
          { "Name": "检查", "Role": "Check", "Scope": "PerItem", "From": ["实施"] }
        ] } ] }
        """;

    private const string TwoPlans = """
        { "Flows": [ { "Name": "默认", "Steps": [
          { "Name": "规划", "Role": "Plan" },
          { "Name": "再规划", "Role": "Plan" },
          { "Name": "实施", "Role": "Implement", "Scope": "PerItem", "From": ["规划"] },
          { "Name": "检查", "Role": "Check", "Scope": "PerItem", "From": ["实施"] }
        ] } ] }
        """;

    private const string AttemptsOverCap = """
        { "Flows": [ { "Name": "默认", "Steps": [
          { "Name": "规划", "Role": "Plan" },
          { "Name": "实施", "Role": "Implement", "Scope": "PerItem", "From": ["规划"] },
          { "Name": "检查", "Role": "Check", "Scope": "PerItem", "From": ["实施"], "MaxAttempts": 9 }
        ] } ] }
        """;

    /// <summary>一条流程只写了名字，步骤缺键时按空步骤处理。</summary>
    private const string NoSteps = """
        { "Flows": [ { "Name": "默认", "Description": "只有名字" } ] }
        """;

    private const string SingleStepPlan = """
        { "Flows": [ { "Name": "默认", "Steps": [
          { "Name": "规划", "Role": "Plan" }
        ] } ] }
        """;

    private const string TwoStepPlan = """
        { "Flows": [
          { "Name": "只规划", "Steps": [ { "Name": "规划", "Role": "Plan" } ] },
          { "Name": "默认", "Steps": [ { "Name": "规划", "Role": "Plan" } ] }
        ] }
        """;
}
