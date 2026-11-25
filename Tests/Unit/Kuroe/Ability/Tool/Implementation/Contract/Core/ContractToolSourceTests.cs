using Xunit;

namespace Kuroe.Ability.Tool.Implementation.Contract.Tests;

/// <summary>契约工具源声明三条固定路径，运行时按执行创建全部载体。</summary>
public sealed class ContractToolSourceTests
{
    [Fact(DisplayName = "定义覆盖三条契约路径")]
    public void Definitions_cover_the_three_contract_paths()
    {
        ContractToolSource source = new();

        Assert.Equal(
            [ContractTools.Port, ContractTools.CannotComplete, ContractTools.Status],
            source.Definitions.Select(definition => definition.Path.Value));
    }
}
