using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.StaticGraph.Nodes;

/// <summary>输出校验：参数边界与五种谓词的判定。</summary>
public sealed class OutputValidationTests
{
    private static readonly PortName First = new("甲");
    private static readonly PortName Second = new("乙");

    [Fact]
    public void NonEmpty_rejects_an_argument()
    {
        Assert.Equal(
            ["NonEmpty 校验不接受参数。"],
            Errors(new OutputValidation(ValidationPredicate.NonEmpty, "多余")));
    }

    [Fact]
    public void Text_predicate_requires_an_argument()
    {
        Assert.Equal(
            ["TextContains 校验需要指定文本参数。"],
            Errors(new OutputValidation(ValidationPredicate.TextContains, null)));
    }

    [Fact]
    public void Pattern_requires_a_valid_regular_expression()
    {
        Assert.Equal(
            ["Pattern 校验的正则不合法：[。"],
            Errors(new OutputValidation(ValidationPredicate.Pattern, "[")));
        Assert.Empty(Errors(new OutputValidation(ValidationPredicate.Pattern, "^ok$")));
    }

    [Fact]
    public void NonEmpty_passes_only_when_a_text_port_has_content()
    {
        OutputValidation validation = new(ValidationPredicate.NonEmpty, null);

        Assert.True(validation.Passes(Outputs(), Values("内容")));
        Assert.False(validation.Passes(Outputs(), Values("")));
        Assert.False(validation.Passes(Outputs(), values: null));
    }

    [Fact]
    public void Text_predicates_compare_the_joined_text()
    {
        Assert.True(new OutputValidation(ValidationPredicate.TextContains, "乙").Passes(Outputs(), Values("甲乙")));
        Assert.True(new OutputValidation(ValidationPredicate.TextNot, "丙").Passes(Outputs(), Values("甲乙")));
        Assert.False(new OutputValidation(ValidationPredicate.TextNot, "乙").Passes(Outputs(), Values("甲乙")));
        Assert.True(new OutputValidation(ValidationPredicate.TextEquals, "甲乙").Passes(Outputs(), Values("甲乙")));
    }

    [Fact]
    public void Pattern_matches_the_joined_text()
    {
        Assert.True(new OutputValidation(ValidationPredicate.Pattern, "^甲\\d+$").Passes(Outputs(), Values("甲123")));
        Assert.False(new OutputValidation(ValidationPredicate.Pattern, "^乙\\d+$").Passes(Outputs(), Values("甲123")));
    }

    [Fact]
    public void Text_ports_are_joined_in_declaration_order()
    {
        OutputValidation validation = new(ValidationPredicate.TextEquals, "甲\n乙");

        Assert.True(validation.Passes(
            [new Port(First, PortTypes.Text), new Port(Second, PortTypes.Text)],
            new Dictionary<PortName, string> { [First] = "甲", [Second] = "乙" }));
    }

    private static IReadOnlyList<Port> Outputs() => [new Port(First, PortTypes.Text)];

    private static Dictionary<PortName, string> Values(string text) =>
        new() { [First] = text };

    private static List<string> Errors(OutputValidation validation)
    {
        List<string> errors = [];
        validation.Check(errors.Add);

        return errors;
    }
}