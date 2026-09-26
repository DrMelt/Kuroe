using System.Text.Json;
using Kuroe.Shared.Agent.Tools;
using Kuroe.TestSupport;
using Kuroe.Tools;
using Xunit;

namespace Kuroe.Tests;

/// <summary>工具载体的函数声明与调用体。</summary>
public sealed class ToolTests
{
    [Fact]
    public void Declared_function_reads_json_arguments()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        ToolFunction submit = new VerdictTool(harness.Submitter).Functions.Single();

        Assert.Equal("SubmitVerdict", submit.Name);
        Assert.NotEmpty(submit.Description);

        string outcome = submit.Invoke(Arguments("""{"passed": false, "findings": "缺验收标准"}"""));

        Assert.Contains("只有进行中的检查 agent", outcome);
    }

    [Fact]
    public void Verdict_missing_passed_is_rejected()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        ToolFunction submit = new VerdictTool(harness.Submitter).Functions.Single();

        string outcome = submit.Invoke(Arguments("""{"findings": "缺验收标准"}"""));

        Assert.Contains("被拒绝：passed 缺失或不是 true/false", outcome);
    }

    /// <summary>实参按模型给出的 JSON 值构造，值各自持有，不随文档释放失效。</summary>
    private static ToolArguments Arguments(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        return new ToolArguments(document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone()));
    }
}
