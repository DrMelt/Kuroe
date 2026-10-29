using Xunit;

namespace Kuroe.Tests;

public sealed class DefaultFlowTests
{
    [Fact]
    public void Default_test_flow_compiles()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        Assert.Empty(environment.Flows.Failures);
    }

    [Fact]
    public void Test_fixture_normalization_moves_start_output_static_to_constant()
    {
        const string json = """{ "Flows": [ { "Start": { "Outputs": [ { "Name": "模型", "Static": true } ] } } ] }""";

        string normalized = TestFlowDocument.Normalize(json);

        Assert.Contains("\"Constant\":true", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Outputs\":[{\"Name\":\"模型\",\"Static\":true}]", normalized, StringComparison.Ordinal);
    }
}
