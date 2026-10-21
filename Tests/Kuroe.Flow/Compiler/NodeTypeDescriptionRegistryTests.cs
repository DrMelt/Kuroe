using Kuroe.Flow.Compiler.Description;
using Kuroe.Flow.Shared.Descriptions;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Xunit;

namespace Kuroe.Flow.Compiler.Tests;

/// <summary>节点类型描述注册表：同名覆盖、查找与注册序。</summary>
public sealed class NodeTypeDescriptionRegistryTests
{
    [Fact]
    public void Register_keeps_the_first_order_and_replaces_the_description()
    {
        NodeTypeDescriptionRegistry registry = new();
        StubDescription first = new("甲");
        StubDescription replacement = new("甲");
        StubDescription second = new("乙");

        registry.Register(first);
        registry.Register(second);
        registry.Register(replacement);

        Assert.Equal([new NodeKind("甲"), new NodeKind("乙")], registry.Kinds);
        Assert.Same(replacement, registry.Find(new NodeKind("甲")));
    }

    [Fact]
    public void Find_by_text_ignores_case_and_missing_kinds_return_null()
    {
        NodeTypeDescriptionRegistry registry = new();
        StubDescription description = new("Executable");
        registry.Register(description);

        Assert.Same(description, registry.Find("executable"));
        Assert.Null(registry.Find("Unknown"));
    }

    private sealed class StubDescription(string kind) : INodeTypeDescription
    {
        public NodeKind Kind { get; } = new(kind);

        public NodeTypeShape Shape { get; } = new();

        public IReadOnlyList<ParameterField> Fields => [];

        public INodeType Read(IParameterReader values, Action<string> fail) => throw new NotSupportedException();
    }
}