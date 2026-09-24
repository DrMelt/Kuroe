using System.Text.Json;
using Kuroe.Agent.Tools;
using Kuroe.Tools;
using Xunit;

namespace Kuroe.Tests;

/// <summary>工具的声明与实参读取。</summary>
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
    public void Arguments_read_text_and_flags()
    {
        ToolArguments arguments = Arguments("""{"city": "北京", "passed": "true", "count": 3}""");

        Assert.Equal("北京", arguments.Text("city"));
        Assert.True(arguments.Flag("passed"));
        Assert.Equal("3", arguments.Text("count"));
        Assert.Null(arguments.Text("missing"));
        Assert.Null(arguments.Flag("city"));
    }

    /// <summary>实参按模型给出的 JSON 值构造，值各自持有，不随文档释放失效。</summary>
    private static ToolArguments Arguments(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        return new ToolArguments(document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone()));
    }
}
