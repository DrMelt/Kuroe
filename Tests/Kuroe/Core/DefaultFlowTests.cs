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

}
